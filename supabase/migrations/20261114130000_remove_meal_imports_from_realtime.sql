-- The app no longer listens to meal_imports over Realtime: the import Functions answer once they have written the row,
-- and the client reads the row when the call returns (and polls it while it is still pending). Nothing subscribes to
-- the table any more, so it leaves the publication it was added to in add_meal_imports_table.

alter publication supabase_realtime drop table public.meal_imports;
