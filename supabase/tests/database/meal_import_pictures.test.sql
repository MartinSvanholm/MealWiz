-- The meal-import-pictures bucket: private, and a user uploads, reads and deletes only inside their own folder
-- ({userId}/...). anon gets nothing. There is no UPDATE policy.
-- Deleting through SQL is blocked by a Storage trigger unless storage.allow_delete_query is set; the app deletes
-- through the Storage API, so this only exercises the policy.
begin;
create extension if not exists pgtap with schema extensions;

select plan(13);

insert into auth.users (id, email, aud, role) values
    ('aaaaaaaa-0000-0000-0000-0000000000a1', 'user-a@pictures.test', 'authenticated', 'authenticated'),
    ('bbbbbbbb-0000-0000-0000-0000000000b2', 'user-b@pictures.test', 'authenticated', 'authenticated');

select results_eq(
    $$ select public, file_size_limit, allowed_mime_types from storage.buckets where id = 'meal-import-pictures' $$,
    $$ values (false, 5242880::bigint, array['image/jpeg']::text[]) $$,
    'the bucket is private, capped at 5 MB, and takes only JPEG'
);

-- B's picture, inserted as postgres (bypasses RLS).
insert into storage.objects (bucket_id, name, owner_id) values
    ('meal-import-pictures', 'bbbbbbbb-0000-0000-0000-0000000000b2/job/0.jpg', 'bbbbbbbb-0000-0000-0000-0000000000b2');

-- As user A.
set local role authenticated;
select set_config('request.jwt.claims', '{"sub":"aaaaaaaa-0000-0000-0000-0000000000a1","role":"authenticated"}', true);

select lives_ok(
    $$ insert into storage.objects (bucket_id, name, owner_id)
       values ('meal-import-pictures', 'aaaaaaaa-0000-0000-0000-0000000000a1/job/0.jpg', 'aaaaaaaa-0000-0000-0000-0000000000a1') $$,
    'user A can upload into their own folder'
);

select throws_ok(
    $$ insert into storage.objects (bucket_id, name, owner_id)
       values ('meal-import-pictures', 'bbbbbbbb-0000-0000-0000-0000000000b2/job/1.jpg', 'aaaaaaaa-0000-0000-0000-0000000000a1') $$,
    '42501', null,
    'user A cannot upload into user B''s folder'
);

select throws_ok(
    $$ insert into storage.objects (bucket_id, name, owner_id)
       values ('meal-import-pictures', 'loose.jpg', 'aaaaaaaa-0000-0000-0000-0000000000a1') $$,
    '42501', null,
    'user A cannot upload outside a folder'
);

select throws_ok(
    $$ insert into storage.objects (bucket_id, name, owner_id)
       values ('avatars', 'aaaaaaaa-0000-0000-0000-0000000000a1/job/0.jpg', 'aaaaaaaa-0000-0000-0000-0000000000a1') $$,
    '42501', null,
    'user A cannot upload into another bucket'
);

select results_eq(
    $$ select name from storage.objects where bucket_id = 'meal-import-pictures' order by name $$,
    $$ values ('aaaaaaaa-0000-0000-0000-0000000000a1/job/0.jpg'::text) $$,
    'user A reads only their own pictures'
);

select is_empty(
    $$ update storage.objects set name = 'aaaaaaaa-0000-0000-0000-0000000000a1/job/9.jpg'
       where bucket_id = 'meal-import-pictures' returning id $$,
    'user A cannot update a picture (no UPDATE policy)'
);

select set_config('storage.allow_delete_query', 'true', true);

select is_empty(
    $$ delete from storage.objects
       where bucket_id = 'meal-import-pictures' and name = 'bbbbbbbb-0000-0000-0000-0000000000b2/job/0.jpg' returning id $$,
    'user A cannot delete user B''s picture'
);

select results_eq(
    $$ delete from storage.objects
       where bucket_id = 'meal-import-pictures' and name = 'aaaaaaaa-0000-0000-0000-0000000000a1/job/0.jpg' returning name $$,
    $$ values ('aaaaaaaa-0000-0000-0000-0000000000a1/job/0.jpg'::text) $$,
    'user A can delete their own picture'
);

-- As user B.
select set_config('request.jwt.claims', '{"sub":"bbbbbbbb-0000-0000-0000-0000000000b2","role":"authenticated"}', true);

select results_eq(
    $$ select name from storage.objects where bucket_id = 'meal-import-pictures' $$,
    $$ values ('bbbbbbbb-0000-0000-0000-0000000000b2/job/0.jpg'::text) $$,
    'user B still has their picture and sees only theirs'
);

-- As anon.
set local role anon;
select set_config('request.jwt.claims', '{"role":"anon"}', true);

select is_empty(
    $$ select id from storage.objects where bucket_id = 'meal-import-pictures' $$,
    'anon reads no pictures'
);

select throws_ok(
    $$ insert into storage.objects (bucket_id, name)
       values ('meal-import-pictures', 'aaaaaaaa-0000-0000-0000-0000000000a1/job/5.jpg') $$,
    '42501', null,
    'anon cannot upload'
);

select is_empty(
    $$ delete from storage.objects where bucket_id = 'meal-import-pictures' returning id $$,
    'anon cannot delete'
);

select * from finish();
rollback;
