alter table "public"."meal_plans" enable row level security;

alter table "public"."meal_plans-meals" enable row level security;

create policy "Delete based on userid"
on "public"."meal_plans"
as permissive
for delete
to public
using ((( SELECT auth.uid() AS uid) = created_by));


create policy "Insert baed on userid"
on "public"."meal_plans"
as permissive
for insert
to public
with check ((( SELECT auth.uid() AS uid) = created_by));


create policy "Select based on userid"
on "public"."meal_plans"
as permissive
for select
to public
using ((( SELECT auth.uid() AS uid) = created_by));


create policy "Updated based on userid"
on "public"."meal_plans"
as permissive
for update
to public
using ((( SELECT auth.uid() AS uid) = created_by))
with check ((( SELECT auth.uid() AS uid) = created_by));


create policy "Delete based on userid"
on "public"."meal_plans-meals"
as permissive
for delete
to public
using ((( SELECT auth.uid() AS uid) = created_by));


create policy "Insert based on userid"
on "public"."meal_plans-meals"
as permissive
for insert
to public
with check ((( SELECT auth.uid() AS uid) = created_by));


create policy "Select based on userid"
on "public"."meal_plans-meals"
as permissive
for select
to public
using ((( SELECT auth.uid() AS uid) = created_by));


create policy "Update based userid"
on "public"."meal_plans-meals"
as permissive
for update
to public
using ((( SELECT auth.uid() AS uid) = created_by))
with check ((( SELECT auth.uid() AS uid) = created_by));



