alter table "public"."ingredients" add column "show_on_grocery_list" boolean not null;

alter table "public"."ingredients" enable row level security;

alter table "public"."meals" add column "name" text not null;

alter table "public"."meals" enable row level security;

alter table "public"."ingredients" add constraint "ingredients_created_by_fkey" FOREIGN KEY (created_by) REFERENCES auth.users(id) ON UPDATE CASCADE ON DELETE CASCADE not valid;

alter table "public"."ingredients" validate constraint "ingredients_created_by_fkey";

alter table "public"."meals" add constraint "meals_created_by_fkey" FOREIGN KEY (created_by) REFERENCES auth.users(id) ON UPDATE CASCADE ON DELETE CASCADE not valid;

alter table "public"."meals" validate constraint "meals_created_by_fkey";

create policy "Enable users to view their own data only"
on "public"."ingredients"
as permissive
for select
to authenticated
using ((( SELECT auth.uid() AS uid) = created_by));


create policy "Enable users to view their own data only"
on "public"."meals"
as permissive
for select
to authenticated
using ((( SELECT auth.uid() AS uid) = created_by));



