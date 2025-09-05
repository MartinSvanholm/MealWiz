alter table "public"."meals" drop column "description";

alter table "public"."meals" add column "recipe" text;

create policy "Enable delete for users based on user_id"
on "public"."ingredients"
as permissive
for delete
to public
using ((( SELECT auth.uid() AS uid) = created_by));


create policy "Enable insert for users based on user_id"
on "public"."ingredients"
as permissive
for insert
to public
with check ((( SELECT auth.uid() AS uid) = created_by));


create policy "Enable update for users based on user_id"
on "public"."ingredients"
as permissive
for update
to public
using ((( SELECT auth.uid() AS uid) = created_by))
with check ((( SELECT auth.uid() AS uid) = created_by));


create policy "Enable delete for users based on user_id"
on "public"."meals"
as permissive
for delete
to public
using ((( SELECT auth.uid() AS uid) = created_by));


create policy "Enable insert for authenticated users only"
on "public"."meals"
as permissive
for insert
to authenticated
with check (true);


create policy "Enable update for users based on user_id"
on "public"."meals"
as permissive
for update
to public
using ((( SELECT auth.uid() AS uid) = created_by))
with check ((( SELECT auth.uid() AS uid) = created_by));



