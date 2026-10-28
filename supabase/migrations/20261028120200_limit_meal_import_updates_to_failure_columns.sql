-- Limit which meal_imports columns a client may update (MEA-31).
-- "Enable users to fail their own pending imports" restricts the rows and the pending -> failed transition, but
-- RLS can't restrict columns, so the same UPDATE could also overwrite source_url, parsed_meal, created_at, etc.
-- Column grants close that: authenticated may only update the columns MarkMealImportFailed sends. Any other
-- column fails with 42501. anon has no UPDATE policy, so it still can't update any row. service_role keeps
-- full access for the import Function.

REVOKE UPDATE ON TABLE "public"."meal_imports" FROM "authenticated";
GRANT UPDATE ("status", "error_message", "updated_at", "updated_by") ON TABLE "public"."meal_imports" TO "authenticated";
