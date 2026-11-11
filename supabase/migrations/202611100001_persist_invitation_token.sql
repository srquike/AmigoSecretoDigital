-- Keep the existing invitation URL recoverable for the authenticated organizer
-- while storing only authenticated-encrypted token data in the database.
alter table public.exchanges
    add column if not exists invitation_token_ciphertext text;

drop function if exists public.rotate_invitation_token(uuid, text);

create or replace function public.rotate_invitation_token(
    p_exchange_id uuid,
    p_token_hash text,
    p_token_ciphertext text
)
returns void
language plpgsql
security definer
set search_path = public, pg_temp
as $$
begin
    if auth.uid() is null then
        raise exception 'Organizer authentication required.' using errcode = '42501';
    end if;

    if p_token_hash is null or p_token_hash !~ '^[0-9a-f]{64}$'
        or p_token_ciphertext is null or char_length(p_token_ciphertext) > 512 then
        raise exception 'Invalid invitation token.' using errcode = '22023';
    end if;

    update public.exchanges
    set invitation_token_hash = p_token_hash,
        invitation_token_ciphertext = p_token_ciphertext,
        invitation_expires_at = now() + interval '90 days'
    where id = p_exchange_id and owner_id = auth.uid() and status = 'RegistrationOpen';

    if not found then
        raise exception 'Exchange is unavailable.' using errcode = 'P0001';
    end if;
end;
$$;

create or replace function public.get_admin_invitation_token(p_exchange_id uuid)
returns table (invitation_token_ciphertext text, invitation_expires_at timestamptz)
language sql
stable
security definer
set search_path = public, pg_temp
as $$
    select e.invitation_token_ciphertext, e.invitation_expires_at
    from public.exchanges e
    where e.id = p_exchange_id
      and e.owner_id = auth.uid()
      and e.status = 'RegistrationOpen'
      and e.invitation_token_hash is not null
      and e.invitation_token_ciphertext is not null
      and e.invitation_expires_at > now();
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
    set status = 'ReadyForDraw',
        invitation_token_hash = null,
        invitation_token_ciphertext = null,
        invitation_expires_at = null
    where id = p_exchange_id;
end;
$$;

revoke all on function public.rotate_invitation_token(uuid, text, text) from public;
revoke all on function public.get_admin_invitation_token(uuid) from public;
grant execute on function public.rotate_invitation_token(uuid, text, text) to authenticated;
grant execute on function public.get_admin_invitation_token(uuid) to authenticated;
