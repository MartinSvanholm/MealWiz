-- Seed data for local development.
-- Re-applied on `supabase start` (first run) and every `supabase db reset`.
-- Only data inserts here, no schema. To deploy to remote: `supabase db push --include-seed`.

-- ---------------------------------------------------------------------------
-- 1. Global system "Leftovers" meal (no owner, visible to all users via RLS).
-- ---------------------------------------------------------------------------
INSERT INTO public.meals (name, recipe, meal_type, created_by, created_at)
SELECT 'Leftovers', '', 'leftover', NULL, now()
WHERE NOT EXISTS (
    SELECT 1 FROM public.meals WHERE meal_type = 'leftover'
);

-- ---------------------------------------------------------------------------
-- 2. Local-only demo user so sample meals have an owner that satisfies the
--    meals.created_by FK to auth.users. Idempotent on the fixed UUID.
-- ---------------------------------------------------------------------------
INSERT INTO auth.users (
    instance_id, id, aud, role, email, encrypted_password,
    email_confirmed_at, raw_app_meta_data, raw_user_meta_data,
    created_at, updated_at,
    confirmation_token, email_change, email_change_token_new, recovery_token
) VALUES (
    '00000000-0000-0000-0000-000000000000',
    '00000000-0000-0000-0000-000000000001',
    'authenticated', 'authenticated',
    'test@test.com',
    crypt('tester', gen_salt('bf')),
    now(),
    '{"provider":"email","providers":["email"]}',
    '{}',
    now(), now(),
    '', '', '', ''
) ON CONFLICT (id) DO NOTHING;

INSERT INTO auth.identities (
    id, user_id, identity_data, provider, provider_id,
    last_sign_in_at, created_at, updated_at
) VALUES (
    '00000000-0000-0000-0000-000000000002',
    '00000000-0000-0000-0000-000000000001',
    '{"sub":"00000000-0000-0000-0000-000000000001","email":"test@test.com"}',
    'email', '00000000-0000-0000-0000-000000000001',
    now(), now(), now()
) ON CONFLICT (id) DO NOTHING;

-- ---------------------------------------------------------------------------
-- 3. Sample meals owned by the demo user, with ingredients.
-- ---------------------------------------------------------------------------
WITH spaghetti AS (
    INSERT INTO public.meals (name, recipe, meal_type, created_by, created_at)
    SELECT
        'Spaghetti Bolognese',
        E'Brown the minced beef.\nAdd onion and garlic, cook until soft.\nStir in tomatoes and simmer 20 minutes.\nServe over cooked spaghetti.',
        'regular',
        '00000000-0000-0000-0000-000000000001',
        now()
    WHERE NOT EXISTS (
        SELECT 1 FROM public.meals
        WHERE name = 'Spaghetti Bolognese'
          AND created_by = '00000000-0000-0000-0000-000000000001'
    )
    RETURNING id
)
INSERT INTO public.ingredients (name, amount, show_on_grocery_list, fk_meal, created_by, created_at)
SELECT v.name, v.amount, true, spaghetti.id, '00000000-0000-0000-0000-000000000001', now()
FROM spaghetti
CROSS JOIN (VALUES
    ('Spaghetti', '400 g'),
    ('Minced beef', '500 g'),
    ('Yellow onion', '1'),
    ('Garlic clove', '2'),
    ('Crushed tomatoes', '400 g')
) AS v(name, amount);

WITH chicken_salad AS (
    INSERT INTO public.meals (name, recipe, meal_type, created_by, created_at)
    SELECT
        'Chicken Caesar Salad',
        E'Grill the chicken breast and slice.\nToss romaine with caesar dressing.\nTop with chicken, parmesan and croutons.',
        'regular',
        '00000000-0000-0000-0000-000000000001',
        now()
    WHERE NOT EXISTS (
        SELECT 1 FROM public.meals
        WHERE name = 'Chicken Caesar Salad'
          AND created_by = '00000000-0000-0000-0000-000000000001'
    )
    RETURNING id
)
INSERT INTO public.ingredients (name, amount, show_on_grocery_list, fk_meal, created_by, created_at)
SELECT v.name, v.amount, true, chicken_salad.id, '00000000-0000-0000-0000-000000000001', now()
FROM chicken_salad
CROSS JOIN (VALUES
    ('Chicken breast', '2'),
    ('Romaine lettuce', '1 head'),
    ('Caesar dressing', '100 ml'),
    ('Parmesan', '50 g'),
    ('Croutons', '1 cup')
) AS v(name, amount);
