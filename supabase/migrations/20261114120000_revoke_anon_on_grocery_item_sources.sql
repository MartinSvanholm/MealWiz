-- grocery_item_sources was created with "grant all ... to anon". No policy lets anon do anything, so RLS already
-- returned nothing, but a signed-out caller has no reason to hold privileges on the table or its sequence at all.
-- The app only reaches it as a signed-in user (authenticated) and nothing else changes.

revoke all on table public.grocery_item_sources from anon;
revoke all on sequence public.grocery_item_sources_id_seq from anon;
