-- An organizer can permanently remove any exchange they own, regardless of status.
-- Foreign keys cascade the deletion to participants, assignments, and reveal links.
create or replace function public.delete_exchange(p_exchange_id uuid)
returns void
language plpgsql
security definer
set search_path = public, pg_temp
as $$
begin
    if auth.uid() is null then
        raise exception 'Organizer authentication required.' using errcode = '42501';
    end if;

    delete from public.exchanges
    where id = p_exchange_id and owner_id = auth.uid();

    if not found then
        raise exception 'Exchange is unavailable.' using errcode = 'P0001';
    end if;
end;
$$;

revoke all on function public.delete_exchange(uuid) from public, anon, authenticated;
grant execute on function public.delete_exchange(uuid) to authenticated;
