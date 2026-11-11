-- Expand the reveal_links row into its columns before assigning it to the row variable.
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
