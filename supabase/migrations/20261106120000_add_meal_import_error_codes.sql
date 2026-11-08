-- MEA-110: why a meal import failed, as a code the client turns into a localized message.
-- - The Function writes invalid_url, no_recipe_found, fetch_failed or unknown together with 'failed'.
-- - The client's own fail path (MarkMealImportFailed) writes unreachable or timed_out.
-- - error_message keeps the English detail, for diagnosis only.
-- The codes are a contract with both apps: MealWiz/tests/Contracts/meal_import_error_code.json.
-- RLS is unchanged; the client's column-level update grant also covers error_code.

alter table public.meal_imports add column error_code text;

alter table public.meal_imports add constraint meal_imports_error_code_check
    check (error_code in ('invalid_url', 'no_recipe_found', 'fetch_failed', 'unreachable', 'timed_out', 'unknown'));

grant update ("error_code") on table public.meal_imports to authenticated;
