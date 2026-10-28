-- Put every existing ingredient back on the grocery list (MEA-38).
-- Until now nothing read show_on_grocery_list, and the app saved ingredients made in the UI with false (only meal
-- imports set true). The grocery list now leaves out ingredients with false, so without this backfill every
-- ingredient made by hand would disappear from "Add plan ingredients". From here the ingredient drawer has a
-- switch for the flag, on by default.

UPDATE "public"."ingredients" SET "show_on_grocery_list" = true WHERE "show_on_grocery_list" = false;
