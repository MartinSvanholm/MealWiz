-- Add meal_imports table backing the "import meal from URL" flow (MEA-11).
-- The client generates the row id itself so it can subscribe to Realtime on
-- the row before invoking the Edge Function that processes the import.
-- Only SELECT/INSERT are exposed to authenticated users; status/parsed_meal/
-- error_message are written exclusively by the service_role Function, which
-- bypasses RLS entirely — see the note above the (deliberately absent)
-- UPDATE policy.

CREATE TABLE IF NOT EXISTS "public"."meal_imports" (
    "id" "uuid" NOT NULL DEFAULT gen_random_uuid(),
    "created_by" "uuid" NOT NULL,
    "source_url" "text" NOT NULL,
    "status" "text" NOT NULL DEFAULT 'pending'
        CHECK ("status" IN ('pending', 'succeeded', 'failed')),
    "parsed_meal" "jsonb",
    "error_message" "text",
    "created_at" timestamp with time zone NOT NULL DEFAULT now(),
    "updated_at" timestamp with time zone,
    "updated_by" "uuid"
);

ALTER TABLE "public"."meal_imports" OWNER TO "postgres";

ALTER TABLE ONLY "public"."meal_imports"
    ADD CONSTRAINT "meal_imports_pkey" PRIMARY KEY ("id");

ALTER TABLE ONLY "public"."meal_imports"
    ADD CONSTRAINT "meal_imports_created_by_fkey" FOREIGN KEY ("created_by") REFERENCES "auth"."users"("id") ON UPDATE CASCADE ON DELETE CASCADE;

ALTER TABLE ONLY "public"."meal_imports"
    ADD CONSTRAINT "meal_imports_updated_by_fkey" FOREIGN KEY ("updated_by") REFERENCES "auth"."users"("id") ON UPDATE CASCADE ON DELETE CASCADE;

ALTER TABLE "public"."meal_imports" ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "Enable users to view their own data only" ON "public"."meal_imports";
CREATE POLICY "Enable users to view their own data only"
    ON "public"."meal_imports"
    FOR SELECT
    TO "authenticated"
    USING (
        (( SELECT "auth"."uid"() AS "uid") = "created_by")
    );

DROP POLICY IF EXISTS "Enable insert for users based on user_id" ON "public"."meal_imports";
CREATE POLICY "Enable insert for users based on user_id"
    ON "public"."meal_imports"
    FOR INSERT
    TO "authenticated"
    WITH CHECK (
        (( SELECT "auth"."uid"() AS "uid") = "created_by")
        AND "status" = 'pending'
    );

-- No UPDATE policy is defined for "authenticated" — this is deliberate, not
-- an oversight. After the initial INSERT, a row's "status"/"parsed_meal"/
-- "error_message" may only be progressed by the service_role Function
-- (service_role bypasses RLS entirely). Authenticated users can only SELECT
-- their row — typically via a Realtime subscription — to observe it change.

GRANT ALL ON TABLE "public"."meal_imports" TO "anon";
GRANT ALL ON TABLE "public"."meal_imports" TO "authenticated";
GRANT ALL ON TABLE "public"."meal_imports" TO "service_role";

-- New territory for this repo: no table has been added to the Realtime
-- publication before now.
ALTER PUBLICATION "supabase_realtime" ADD TABLE "public"."meal_imports";
