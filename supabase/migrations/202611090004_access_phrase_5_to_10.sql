-- New participant phrases are 5-10 characters. Existing hashes remain verifiable by reveal_assignment.
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

grant execute on function public.register_participant(text, text, smallint, text, text) to anon, authenticated;
