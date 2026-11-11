-- Treat expected_participants as an estimate. Registration remains open until the organizer closes it,
-- and the draw can proceed with any count from 3 to the application-wide maximum of 100.

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

grant execute on function public.get_invitation_context(text) to anon, authenticated;
grant execute on function public.register_participant(text, text, smallint, text, text) to anon, authenticated;
grant execute on function public.close_registration(uuid) to authenticated;
grant execute on function public.draw_exchange(uuid) to authenticated;
