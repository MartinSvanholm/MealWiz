-- The daily picture clean-up (CleanUpMealPictures Function) needs the pictures older than a cutoff. Storage can't list
-- objects by age, and objects must never be deleted with SQL (a trigger blocks it and it would orphan the file), so this
-- function only *names* them; the Function removes them through the Storage API. Only the service role may call it.

create or replace function public.expired_meal_import_pictures(cutoff timestamptz)
returns setof text
language sql
stable
security definer
set search_path = ''
as $$
    select o.name
    from storage.objects o
    where o.bucket_id = 'meal-import-pictures'
      and o.created_at < cutoff
    order by o.created_at
$$;

revoke all on function public.expired_meal_import_pictures(timestamptz) from public, anon, authenticated;
grant execute on function public.expired_meal_import_pictures(timestamptz) to service_role;
