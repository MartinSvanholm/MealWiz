revoke delete on table "public"."grocery_items" from "anon";

revoke insert on table "public"."grocery_items" from "anon";

revoke references on table "public"."grocery_items" from "anon";

revoke select on table "public"."grocery_items" from "anon";

revoke trigger on table "public"."grocery_items" from "anon";

revoke truncate on table "public"."grocery_items" from "anon";

revoke update on table "public"."grocery_items" from "anon";

revoke delete on table "public"."grocery_items" from "authenticated";

revoke insert on table "public"."grocery_items" from "authenticated";

revoke references on table "public"."grocery_items" from "authenticated";

revoke select on table "public"."grocery_items" from "authenticated";

revoke trigger on table "public"."grocery_items" from "authenticated";

revoke truncate on table "public"."grocery_items" from "authenticated";

revoke update on table "public"."grocery_items" from "authenticated";

revoke delete on table "public"."grocery_items" from "service_role";

revoke insert on table "public"."grocery_items" from "service_role";

revoke references on table "public"."grocery_items" from "service_role";

revoke select on table "public"."grocery_items" from "service_role";

revoke trigger on table "public"."grocery_items" from "service_role";

revoke truncate on table "public"."grocery_items" from "service_role";

revoke update on table "public"."grocery_items" from "service_role";

revoke delete on table "public"."ingredients" from "anon";

revoke insert on table "public"."ingredients" from "anon";

revoke references on table "public"."ingredients" from "anon";

revoke select on table "public"."ingredients" from "anon";

revoke trigger on table "public"."ingredients" from "anon";

revoke truncate on table "public"."ingredients" from "anon";

revoke update on table "public"."ingredients" from "anon";

revoke delete on table "public"."ingredients" from "authenticated";

revoke insert on table "public"."ingredients" from "authenticated";

revoke references on table "public"."ingredients" from "authenticated";

revoke select on table "public"."ingredients" from "authenticated";

revoke trigger on table "public"."ingredients" from "authenticated";

revoke truncate on table "public"."ingredients" from "authenticated";

revoke update on table "public"."ingredients" from "authenticated";

revoke delete on table "public"."ingredients" from "service_role";

revoke insert on table "public"."ingredients" from "service_role";

revoke references on table "public"."ingredients" from "service_role";

revoke select on table "public"."ingredients" from "service_role";

revoke trigger on table "public"."ingredients" from "service_role";

revoke truncate on table "public"."ingredients" from "service_role";

revoke update on table "public"."ingredients" from "service_role";

revoke delete on table "public"."meal_plans" from "anon";

revoke insert on table "public"."meal_plans" from "anon";

revoke references on table "public"."meal_plans" from "anon";

revoke select on table "public"."meal_plans" from "anon";

revoke trigger on table "public"."meal_plans" from "anon";

revoke truncate on table "public"."meal_plans" from "anon";

revoke update on table "public"."meal_plans" from "anon";

revoke delete on table "public"."meal_plans" from "authenticated";

revoke insert on table "public"."meal_plans" from "authenticated";

revoke references on table "public"."meal_plans" from "authenticated";

revoke select on table "public"."meal_plans" from "authenticated";

revoke trigger on table "public"."meal_plans" from "authenticated";

revoke truncate on table "public"."meal_plans" from "authenticated";

revoke update on table "public"."meal_plans" from "authenticated";

revoke delete on table "public"."meal_plans" from "service_role";

revoke insert on table "public"."meal_plans" from "service_role";

revoke references on table "public"."meal_plans" from "service_role";

revoke select on table "public"."meal_plans" from "service_role";

revoke trigger on table "public"."meal_plans" from "service_role";

revoke truncate on table "public"."meal_plans" from "service_role";

revoke update on table "public"."meal_plans" from "service_role";

revoke delete on table "public"."meal_plans-meals" from "anon";

revoke insert on table "public"."meal_plans-meals" from "anon";

revoke references on table "public"."meal_plans-meals" from "anon";

revoke select on table "public"."meal_plans-meals" from "anon";

revoke trigger on table "public"."meal_plans-meals" from "anon";

revoke truncate on table "public"."meal_plans-meals" from "anon";

revoke update on table "public"."meal_plans-meals" from "anon";

revoke delete on table "public"."meal_plans-meals" from "authenticated";

revoke insert on table "public"."meal_plans-meals" from "authenticated";

revoke references on table "public"."meal_plans-meals" from "authenticated";

revoke select on table "public"."meal_plans-meals" from "authenticated";

revoke trigger on table "public"."meal_plans-meals" from "authenticated";

revoke truncate on table "public"."meal_plans-meals" from "authenticated";

revoke update on table "public"."meal_plans-meals" from "authenticated";

revoke delete on table "public"."meal_plans-meals" from "service_role";

revoke insert on table "public"."meal_plans-meals" from "service_role";

revoke references on table "public"."meal_plans-meals" from "service_role";

revoke select on table "public"."meal_plans-meals" from "service_role";

revoke trigger on table "public"."meal_plans-meals" from "service_role";

revoke truncate on table "public"."meal_plans-meals" from "service_role";

revoke update on table "public"."meal_plans-meals" from "service_role";

revoke delete on table "public"."meals" from "anon";

revoke insert on table "public"."meals" from "anon";

revoke references on table "public"."meals" from "anon";

revoke select on table "public"."meals" from "anon";

revoke trigger on table "public"."meals" from "anon";

revoke truncate on table "public"."meals" from "anon";

revoke update on table "public"."meals" from "anon";

revoke delete on table "public"."meals" from "authenticated";

revoke insert on table "public"."meals" from "authenticated";

revoke references on table "public"."meals" from "authenticated";

revoke select on table "public"."meals" from "authenticated";

revoke trigger on table "public"."meals" from "authenticated";

revoke truncate on table "public"."meals" from "authenticated";

revoke update on table "public"."meals" from "authenticated";

revoke delete on table "public"."meals" from "service_role";

revoke insert on table "public"."meals" from "service_role";

revoke references on table "public"."meals" from "service_role";

revoke select on table "public"."meals" from "service_role";

revoke trigger on table "public"."meals" from "service_role";

revoke truncate on table "public"."meals" from "service_role";

revoke update on table "public"."meals" from "service_role";


CREATE TRIGGER enforce_bucket_name_length_trigger BEFORE INSERT OR UPDATE OF name ON storage.buckets FOR EACH ROW EXECUTE FUNCTION storage.enforce_bucket_name_length();

CREATE TRIGGER objects_delete_delete_prefix AFTER DELETE ON storage.objects FOR EACH ROW EXECUTE FUNCTION storage.delete_prefix_hierarchy_trigger();

CREATE TRIGGER objects_insert_create_prefix BEFORE INSERT ON storage.objects FOR EACH ROW EXECUTE FUNCTION storage.objects_insert_prefix_trigger();

CREATE TRIGGER objects_update_create_prefix BEFORE UPDATE ON storage.objects FOR EACH ROW WHEN (((new.name <> old.name) OR (new.bucket_id <> old.bucket_id))) EXECUTE FUNCTION storage.objects_update_prefix_trigger();

CREATE TRIGGER update_objects_updated_at BEFORE UPDATE ON storage.objects FOR EACH ROW EXECUTE FUNCTION storage.update_updated_at_column();

CREATE TRIGGER prefixes_create_hierarchy BEFORE INSERT ON storage.prefixes FOR EACH ROW WHEN ((pg_trigger_depth() < 1)) EXECUTE FUNCTION storage.prefixes_insert_trigger();

CREATE TRIGGER prefixes_delete_hierarchy AFTER DELETE ON storage.prefixes FOR EACH ROW EXECUTE FUNCTION storage.delete_prefix_hierarchy_trigger();


