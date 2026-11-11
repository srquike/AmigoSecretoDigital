-- Initial schema for Amigo Secreto Digital.
-- Apply from the Supabase SQL Editor as the project owner. Do not run as anon/authenticated.

create schema if not exists extensions;
create extension if not exists pgcrypto with schema extensions;

create table if not exists public.exchanges (
    id uuid primary key default gen_random_uuid(),
    owner_id uuid not null references auth.users(id) on delete cascade,
    name text not null check (char_length(btrim(name)) between 1 and 80),
    event_date date not null,
    conditions text not null default '' check (char_length(conditions) <= 2000),
    budget numeric(10, 2) check (budget is null or budget >= 0),
    expected_participants integer not null check (expected_participants between 3 and 100),
    status text not null default 'RegistrationOpen'
        check (status in ('Draft', 'RegistrationOpen', 'ReadyForDraw', 'Drawn', 'Cancelled')),
    invitation_token_hash text unique,
    invitation_expires_at timestamptz,
    created_at timestamptz not null default now()
);

create index if not exists ix_exchanges_owner_event_date
    on public.exchanges(owner_id, event_date desc);

create table if not exists public.participants (
    id uuid primary key default gen_random_uuid(),
    exchange_id uuid not null references public.exchanges(id) on delete cascade,
    display_name text not null check (char_length(btrim(display_name)) between 1 and 60),
    normalized_name text generated always as (lower(btrim(display_name))) stored,
    age smallint not null check (age between 1 and 120),
    sex text not null check (sex in ('NotSpecified', 'Female', 'Male', 'Other')),
    private_phrase_hash text not null,
    registered_at timestamptz not null default now(),
    unique (exchange_id, id)
);

create unique index if not exists ux_participants_exchange_normalized_name
    on public.participants(exchange_id, normalized_name);

create table if not exists public.assignments (
    exchange_id uuid not null references public.exchanges(id) on delete cascade,
    giver_id uuid not null,
    recipient_id uuid not null,
    primary key (exchange_id, giver_id),
    unique (exchange_id, recipient_id),
    check (giver_id <> recipient_id),
    foreign key (exchange_id, giver_id) references public.participants(exchange_id, id) on delete cascade,
    foreign key (exchange_id, recipient_id) references public.participants(exchange_id, id) on delete cascade
);

create table if not exists public.reveal_links (
    id uuid primary key default gen_random_uuid(),
    exchange_id uuid not null,
    participant_id uuid not null,
    token_hash text not null unique,
    created_at timestamptz not null default now(),
    expires_at timestamptz not null,
    revoked_at timestamptz,
    failed_attempts smallint not null default 0 check (failed_attempts between 0 and 8),
    locked_until timestamptz,
    foreign key (exchange_id, participant_id)
        references public.participants(exchange_id, id) on delete cascade
);

create index if not exists ix_reveal_links_exchange_participant
    on public.reveal_links(exchange_id, participant_id) where revoked_at is null;

alter table public.exchanges enable row level security;
alter table public.participants enable row level security;
alter table public.assignments enable row level security;
alter table public.reveal_links enable row level security;

-- Data is accessed only through the narrowly-scoped functions below.
revoke all on public.exchanges, public.participants, public.assignments, public.reveal_links from anon, authenticated;
grant usage on schema public to anon, authenticated;

create or replace function public.create_exchange(
    p_name text,
    p_event_date date,
    p_conditions text,
    p_budget numeric,
    p_expected_participants integer
)
returns table (
    exchange_id uuid,
    exchange_owner_id uuid,
    exchange_name text,
    exchange_event_date date,
    exchange_conditions text,
    exchange_budget numeric,
    exchange_expected_participants integer,
    exchange_status text,
    exchange_created_at timestamptz
)
language plpgsql
security definer
set search_path = public, extensions, pg_temp
as $$
declare
    v_id uuid;
begin
    if auth.uid() is null then
        raise exception 'Organizer authentication required.' using errcode = '42501';
    end if;

    if char_length(btrim(coalesce(p_name, ''))) not between 1 and 80
        or p_event_date is null or p_event_date < current_date
        or char_length(coalesce(p_conditions, '')) > 2000
        or p_expected_participants is null or p_expected_participants not between 3 and 100
        or (p_budget is not null and p_budget < 0) then
        raise exception 'Invalid exchange details.' using errcode = '22023';
    end if;

    insert into public.exchanges (owner_id, name, event_date, conditions, budget, expected_participants)
    values (auth.uid(), btrim(p_name), p_event_date, coalesce(p_conditions, ''), p_budget, p_expected_participants)
    returning id into v_id;

    return query
    select e.id, e.owner_id, e.name, e.event_date, e.conditions, e.budget, e.expected_participants, e.status, e.created_at
    from public.exchanges e where e.id = v_id;
end;
$$;

create or replace function public.list_admin_exchanges()
returns table (
    exchange_id uuid,
    exchange_owner_id uuid,
    exchange_name text,
    exchange_event_date date,
    exchange_conditions text,
    exchange_budget numeric,
    exchange_expected_participants integer,
    exchange_status text,
    exchange_created_at timestamptz,
    participant_count bigint
)
language sql
stable
security definer
set search_path = public, pg_temp
as $$
    select e.id, e.owner_id, e.name, e.event_date, e.conditions, e.budget, e.expected_participants, e.status, e.created_at,
           count(p.id)
    from public.exchanges e
    left join public.participants p on p.exchange_id = e.id
    where e.owner_id = auth.uid()
    group by e.id
    order by e.event_date desc, e.created_at desc;
$$;

create or replace function public.get_admin_exchange(p_exchange_id uuid)
returns table (
    exchange_id uuid,
    exchange_owner_id uuid,
    exchange_name text,
    exchange_event_date date,
    exchange_conditions text,
    exchange_budget numeric,
    exchange_expected_participants integer,
    exchange_status text,
    exchange_created_at timestamptz,
    participant_count bigint
)
language sql
stable
security definer
set search_path = public, pg_temp
as $$
    select e.id, e.owner_id, e.name, e.event_date, e.conditions, e.budget, e.expected_participants, e.status, e.created_at,
           count(p.id)
    from public.exchanges e
    left join public.participants p on p.exchange_id = e.id
    where e.owner_id = auth.uid() and e.id = p_exchange_id
    group by e.id;
$$;

create or replace function public.get_invitation_context(p_invitation_token text)
returns table (
    exchange_id uuid,
    exchange_name text,
    exchange_event_date date,
    exchange_conditions text,
    exchange_budget numeric,
    exchange_expected_participants integer,
    participant_count bigint
)
language sql
stable
security definer
set search_path = public, extensions, pg_temp
as $$
    select e.id, e.name, e.event_date, e.conditions, e.budget, e.expected_participants,
           (select count(*) from public.participants p where p.exchange_id = e.id)
    from public.exchanges e
    where e.invitation_token_hash = encode(digest(convert_to(coalesce(p_invitation_token, ''), 'UTF8'), 'sha256'), 'hex')
      and e.status = 'RegistrationOpen'
      and e.invitation_expires_at > now()
      and (select count(*) from public.participants p where p.exchange_id = e.id) < 100;
$$;

create or replace function public.rotate_invitation_token(p_exchange_id uuid, p_token_hash text)
returns void
language plpgsql
security definer
set search_path = public, pg_temp
as $$
begin
    if auth.uid() is null then
        raise exception 'Organizer authentication required.' using errcode = '42501';
    end if;

    update public.exchanges
    set invitation_token_hash = p_token_hash,
        invitation_expires_at = now() + interval '90 days'
    where id = p_exchange_id and owner_id = auth.uid() and status = 'RegistrationOpen';

    if not found then
        raise exception 'Exchange is unavailable.' using errcode = 'P0001';
    end if;
end;
$$;

create or replace function public.register_participant(
    p_invitation_token text,
    p_display_name text,
    p_age smallint,
    p_sex text,
    p_private_phrase text
)
returns table (participant_id uuid, participant_exchange_id uuid, participant_registered_at timestamptz)
language plpgsql
security definer
set search_path = public, extensions, pg_temp
as $$
declare
    v_exchange public.exchanges%rowtype;
    v_count integer;
    v_participant_id uuid;
begin
    if char_length(btrim(coalesce(p_display_name, ''))) not between 1 and 60
        or p_age not between 1 and 120
        or p_sex not in ('NotSpecified', 'Female', 'Male', 'Other')
        or char_length(btrim(coalesce(p_private_phrase, ''))) not between 5 and 10 then
        raise exception 'Invalid participant details.' using errcode = '22023';
    end if;

    select e.* into v_exchange
    from public.exchanges e
    where e.invitation_token_hash = encode(digest(convert_to(coalesce(p_invitation_token, ''), 'UTF8'), 'sha256'), 'hex')
      and e.invitation_expires_at > now()
    for update;

    if not found or v_exchange.status <> 'RegistrationOpen' then
        raise exception 'Invitation is unavailable.' using errcode = 'P0001';
    end if;

    select count(*) into v_count from public.participants p where p.exchange_id = v_exchange.id;
    if v_count >= 100 then
        raise exception 'Exchange reached the maximum participant count.' using errcode = 'P0001';
    end if;

    if exists (
        select 1 from public.participants p
        where p.exchange_id = v_exchange.id and p.normalized_name = lower(btrim(p_display_name))
    ) then
        raise exception 'Participant name already registered.' using errcode = '23505';
    end if;

    insert into public.participants (exchange_id, display_name, age, sex, private_phrase_hash)
    values (
        v_exchange.id,
        btrim(p_display_name),
        p_age,
        p_sex,
        crypt(btrim(p_private_phrase), gen_salt('bf', 12))
    )
    returning id into v_participant_id;

    return query select p.id, p.exchange_id, p.registered_at
    from public.participants p where p.id = v_participant_id;
end;
$$;

create or replace function public.list_admin_participants(p_exchange_id uuid)
returns table (participant_id uuid, display_name text, registered_at timestamptz)
language sql
stable
security definer
set search_path = public, pg_temp
as $$
    select p.id, p.display_name, p.registered_at
    from public.participants p
    join public.exchanges e on e.id = p.exchange_id
    where p.exchange_id = p_exchange_id and e.owner_id = auth.uid()
    order by p.registered_at;
$$;

create or replace function public.close_registration(p_exchange_id uuid)
returns void
language plpgsql
security definer
set search_path = public, pg_temp
as $$
declare
    v_count integer;
begin
    if auth.uid() is null then
        raise exception 'Organizer authentication required.' using errcode = '42501';
    end if;

    perform 1
    from public.exchanges e
    where e.id = p_exchange_id and e.owner_id = auth.uid() and e.status = 'RegistrationOpen'
    for update;

    if not found then
        raise exception 'Exchange is unavailable.' using errcode = 'P0001';
    end if;

    select count(*) into v_count from public.participants p where p.exchange_id = p_exchange_id;
    if v_count < 3 then
        raise exception 'At least three participants are required.' using errcode = 'P0001';
    end if;

    update public.exchanges
    set status = 'ReadyForDraw', invitation_token_hash = null, invitation_expires_at = null
    where id = p_exchange_id;
end;
$$;

create or replace function public.draw_exchange(p_exchange_id uuid)
returns void
language plpgsql
security definer
set search_path = public, pg_temp
as $$
declare
    v_exchange public.exchanges%rowtype;
    v_ids uuid[];
    v_count integer;
    v_index integer;
begin
    if auth.uid() is null then
        raise exception 'Organizer authentication required.' using errcode = '42501';
    end if;

    select e.* into v_exchange
    from public.exchanges e
    where e.id = p_exchange_id and e.owner_id = auth.uid() and e.status = 'ReadyForDraw'
    for update;

    if not found then
        raise exception 'Exchange is not ready for drawing.' using errcode = 'P0001';
    end if;

    select array_agg(p.id order by random()) into v_ids
    from public.participants p where p.exchange_id = p_exchange_id;
    v_count := coalesce(array_length(v_ids, 1), 0);

    if v_count < 3 or v_count > 100 then
        raise exception 'The participant count is invalid.' using errcode = 'P0001';
    end if;

    for v_index in 1..v_count loop
        insert into public.assignments(exchange_id, giver_id, recipient_id)
        values (p_exchange_id, v_ids[v_index], v_ids[(v_index % v_count) + 1]);
    end loop;

    update public.exchanges set status = 'Drawn' where id = p_exchange_id;
end;
$$;

create or replace function public.get_admin_assignments(p_exchange_id uuid)
returns table (participant_id uuid, participant_name text, recipient_name text)
language sql
stable
security definer
set search_path = public, pg_temp
as $$
    select giver.id, giver.display_name, recipient.display_name
    from public.assignments a
    join public.exchanges e on e.id = a.exchange_id
    join public.participants giver on giver.id = a.giver_id and giver.exchange_id = a.exchange_id
    join public.participants recipient on recipient.id = a.recipient_id and recipient.exchange_id = a.exchange_id
    where a.exchange_id = p_exchange_id and e.owner_id = auth.uid()
    order by giver.display_name;
$$;

create or replace function public.create_reveal_link(
    p_exchange_id uuid,
    p_participant_id uuid,
    p_token_hash text
)
returns void
language plpgsql
security definer
set search_path = public, pg_temp
as $$
begin
    if auth.uid() is null or not exists (
        select 1 from public.exchanges e
        join public.assignments a on a.exchange_id = e.id
        where e.id = p_exchange_id and e.owner_id = auth.uid() and e.status = 'Drawn'
          and a.giver_id = p_participant_id
    ) then
        raise exception 'Assignment is unavailable.' using errcode = 'P0001';
    end if;

    update public.reveal_links
    set revoked_at = now()
    where exchange_id = p_exchange_id and participant_id = p_participant_id and revoked_at is null;

    insert into public.reveal_links(exchange_id, participant_id, token_hash, expires_at)
    values (p_exchange_id, p_participant_id, p_token_hash, now() + interval '90 days');
end;
$$;

create or replace function public.reveal_assignment(p_reveal_token text, p_private_phrase text)
returns table (recipient_name text)
language plpgsql
security definer
set search_path = public, extensions, pg_temp
as $$
declare
    v_link public.reveal_links%rowtype;
    v_phrase_hash text;
    v_recipient_name text;
begin
    select l.* into v_link
    from public.reveal_links l
    join public.participants p on p.id = l.participant_id and p.exchange_id = l.exchange_id
    join public.exchanges e on e.id = l.exchange_id and e.status = 'Drawn'
    where l.token_hash = encode(digest(convert_to(coalesce(p_reveal_token, ''), 'UTF8'), 'sha256'), 'hex')
      and l.revoked_at is null
      and l.expires_at > now()
    for update of l;

    if not found or (v_link.locked_until is not null and v_link.locked_until > now()) then
        return;
    end if;

    select p.private_phrase_hash into v_phrase_hash
    from public.participants p
    where p.id = v_link.participant_id and p.exchange_id = v_link.exchange_id;

    if crypt(coalesce(p_private_phrase, ''), v_phrase_hash) is distinct from v_phrase_hash then
        update public.reveal_links
        set failed_attempts = least(failed_attempts + 1, 8),
            locked_until = case when failed_attempts + 1 >= 8 then now() + interval '15 minutes' else null end
        where id = v_link.id;
        return;
    end if;

    update public.reveal_links set failed_attempts = 0, locked_until = null where id = v_link.id;

    select recipient.display_name into v_recipient_name
    from public.assignments a
    join public.participants recipient on recipient.id = a.recipient_id and recipient.exchange_id = a.exchange_id
    where a.exchange_id = v_link.exchange_id and a.giver_id = v_link.participant_id;

    if v_recipient_name is null then
        raise exception 'Reveal is unavailable.' using errcode = 'P0001';
    end if;

    return query select v_recipient_name;
end;
$$;

revoke all on function public.create_exchange(text, date, text, numeric, integer) from public;
revoke all on function public.list_admin_exchanges() from public;
revoke all on function public.get_admin_exchange(uuid) from public;
revoke all on function public.get_invitation_context(text) from public;
revoke all on function public.rotate_invitation_token(uuid, text) from public;
revoke all on function public.register_participant(text, text, smallint, text, text) from public;
revoke all on function public.list_admin_participants(uuid) from public;
revoke all on function public.close_registration(uuid) from public;
revoke all on function public.draw_exchange(uuid) from public;
revoke all on function public.get_admin_assignments(uuid) from public;
revoke all on function public.create_reveal_link(uuid, uuid, text) from public;
revoke all on function public.reveal_assignment(text, text) from public;

grant execute on function public.create_exchange(text, date, text, numeric, integer) to authenticated;
grant execute on function public.list_admin_exchanges() to authenticated;
grant execute on function public.get_admin_exchange(uuid) to authenticated;
grant execute on function public.get_invitation_context(text) to anon, authenticated;
grant execute on function public.rotate_invitation_token(uuid, text) to authenticated;
grant execute on function public.register_participant(text, text, smallint, text, text) to anon, authenticated;
grant execute on function public.list_admin_participants(uuid) to authenticated;
grant execute on function public.close_registration(uuid) to authenticated;
grant execute on function public.draw_exchange(uuid) to authenticated;
grant execute on function public.get_admin_assignments(uuid) to authenticated;
grant execute on function public.create_reveal_link(uuid, uuid, text) to authenticated;
grant execute on function public.reveal_assignment(text, text) to anon, authenticated;
