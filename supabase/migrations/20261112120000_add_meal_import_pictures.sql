-- Meal creation from a picture: the pictures bucket, and the picture job on meal_imports.
--
-- A meal_imports row is a link job (source_url) or a picture job (picture_paths, language, optional hint), never
-- both or neither. The client inserts a picture job as 'pending' (the existing INSERT policy); its column-level UPDATE
-- grant doesn't cover the new columns, so it can't change them afterwards. The AnalyzeMealPicture Function (service
-- role) resolves the row.
--
-- The bucket is private and each user reaches only their own folder: {userId}/{jobId}/{index}.jpg. There is no UPDATE
-- policy. Never `alter table storage.objects` (owned by supabase_storage_admin) and never delete objects with SQL
-- (a trigger blocks it, and it would orphan the file): policies only, and the Storage API for deletes.
-- The error codes are a contract with both apps: MealWiz/tests/Contracts/meal_import_error_code.json.

-- Bucket.
insert into storage.buckets (id, name, public, file_size_limit, allowed_mime_types)
values ('meal-import-pictures', 'meal-import-pictures', false, 5242880, array['image/jpeg'])
on conflict (id) do nothing;

create policy "Meal import pictures: owner inserts"
    on storage.objects for insert to authenticated
    with check (
        bucket_id = 'meal-import-pictures'
        and (storage.foldername(name))[1] = (select auth.uid()::text)
    );

create policy "Meal import pictures: owner reads"
    on storage.objects for select to authenticated
    using (
        bucket_id = 'meal-import-pictures'
        and (storage.foldername(name))[1] = (select auth.uid()::text)
    );

create policy "Meal import pictures: owner deletes"
    on storage.objects for delete to authenticated
    using (
        bucket_id = 'meal-import-pictures'
        and (storage.foldername(name))[1] = (select auth.uid()::text)
    );

-- Picture job columns.
alter table public.meal_imports alter column source_url drop not null;
alter table public.meal_imports add column picture_paths text[];
alter table public.meal_imports add column language text;
alter table public.meal_imports add column hint text;

alter table public.meal_imports add constraint meal_imports_kind_check check (
    (source_url is not null and picture_paths is null and hint is null)
    or (source_url is null and picture_paths is not null
        and cardinality(picture_paths) between 1 and 3 and language is not null)
);
alter table public.meal_imports add constraint meal_imports_language_check check (language in ('en', 'da'));
alter table public.meal_imports add constraint meal_imports_hint_check check (char_length(hint) between 1 and 200);

alter table public.meal_imports drop constraint meal_imports_error_code_check;
alter table public.meal_imports add constraint meal_imports_error_code_check check (error_code in (
    'invalid_url', 'no_recipe_found', 'fetch_failed', 'unreachable', 'timed_out', 'unknown',
    'no_meal_found', 'daily_limit_reached', 'picture_unreadable', 'ai_unavailable'));
