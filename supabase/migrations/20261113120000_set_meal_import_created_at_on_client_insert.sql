-- meal_imports.created_at is the server's time for every row a client inserts.
--
-- The AnalyzeMealPicture Function trusts created_at twice: a picture job must be at most 5 minutes old (and at most
-- 1 minute ahead), and the daily limit counts the owner's picture jobs created in the current UTC day. A client that
-- wrote created_at from its own clock would fail every picture import on a device whose clock is off, and could place
-- a job outside "today" to get past the limit. So an insert as authenticated (or anon) always gets now(), whatever it
-- sends. The service role and the table owner (the Function, migrations, test fixtures) keep the value they give.

create or replace function public.meal_imports_set_created_at()
returns trigger
language plpgsql
set search_path = ''
as $$
begin
    if current_user in ('authenticated', 'anon') then
        new.created_at := now();
    end if;
    return new;
end;
$$;

revoke all on function public.meal_imports_set_created_at() from public, anon, authenticated;

create trigger meal_imports_set_created_at
    before insert on public.meal_imports
    for each row execute function public.meal_imports_set_created_at();
