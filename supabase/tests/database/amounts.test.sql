-- Amounts as a quantity and a unit (add_amount_quantity_and_unit): public.parse_amount agrees with the contract
-- TheMealWorkshop/tests/Contracts/amount_parsing.json (the C# parser is tested against the same file, and a shared-unit test
-- checks that the copy below equals it), the columns' checks, who may call the function, and the migration's backfill.
-- Everything is rolled back.
begin;
create extension if not exists pgtap with schema extensions;

select plan(21);

-- The contract, pasted as one jsonb literal between the markers.
-- contract:begin
create temp table contract as
select c.text, c.quantity, c.unit
from jsonb_to_recordset($contract${"units":["g","kg","oz","lb","ml","dl","l","cup","tsk","spsk"],"cases":[{"text":"2 g","quantity":2,"unit":"g"},{"text":"2g.","quantity":2,"unit":"g"},{"text":"1,5 G","quantity":1.5,"unit":"g"},{"text":"0.25 G.","quantity":0.25,"unit":"g"},{"text":"2 gr","quantity":2,"unit":"g"},{"text":"2gr.","quantity":2,"unit":"g"},{"text":"1,5 GR","quantity":1.5,"unit":"g"},{"text":"0.25 Gr.","quantity":0.25,"unit":"g"},{"text":"2 gram","quantity":2,"unit":"g"},{"text":"2gram.","quantity":2,"unit":"g"},{"text":"1,5 GRAM","quantity":1.5,"unit":"g"},{"text":"0.25 Gram.","quantity":0.25,"unit":"g"},{"text":"2 grams","quantity":2,"unit":"g"},{"text":"2grams.","quantity":2,"unit":"g"},{"text":"1,5 GRAMS","quantity":1.5,"unit":"g"},{"text":"0.25 Grams.","quantity":0.25,"unit":"g"},{"text":"2 kg","quantity":2,"unit":"kg"},{"text":"2kg.","quantity":2,"unit":"kg"},{"text":"1,5 KG","quantity":1.5,"unit":"kg"},{"text":"0.25 Kg.","quantity":0.25,"unit":"kg"},{"text":"2 kilo","quantity":2,"unit":"kg"},{"text":"2kilo.","quantity":2,"unit":"kg"},{"text":"1,5 KILO","quantity":1.5,"unit":"kg"},{"text":"0.25 Kilo.","quantity":0.25,"unit":"kg"},{"text":"2 kilos","quantity":2,"unit":"kg"},{"text":"2kilos.","quantity":2,"unit":"kg"},{"text":"1,5 KILOS","quantity":1.5,"unit":"kg"},{"text":"0.25 Kilos.","quantity":0.25,"unit":"kg"},{"text":"2 kilogram","quantity":2,"unit":"kg"},{"text":"2kilogram.","quantity":2,"unit":"kg"},{"text":"1,5 KILOGRAM","quantity":1.5,"unit":"kg"},{"text":"0.25 Kilogram.","quantity":0.25,"unit":"kg"},{"text":"2 kilograms","quantity":2,"unit":"kg"},{"text":"2kilograms.","quantity":2,"unit":"kg"},{"text":"1,5 KILOGRAMS","quantity":1.5,"unit":"kg"},{"text":"0.25 Kilograms.","quantity":0.25,"unit":"kg"},{"text":"2 oz","quantity":2,"unit":"oz"},{"text":"2oz.","quantity":2,"unit":"oz"},{"text":"1,5 OZ","quantity":1.5,"unit":"oz"},{"text":"0.25 Oz.","quantity":0.25,"unit":"oz"},{"text":"2 ounce","quantity":2,"unit":"oz"},{"text":"2ounce.","quantity":2,"unit":"oz"},{"text":"1,5 OUNCE","quantity":1.5,"unit":"oz"},{"text":"0.25 Ounce.","quantity":0.25,"unit":"oz"},{"text":"2 ounces","quantity":2,"unit":"oz"},{"text":"2ounces.","quantity":2,"unit":"oz"},{"text":"1,5 OUNCES","quantity":1.5,"unit":"oz"},{"text":"0.25 Ounces.","quantity":0.25,"unit":"oz"},{"text":"2 lb","quantity":2,"unit":"lb"},{"text":"2lb.","quantity":2,"unit":"lb"},{"text":"1,5 LB","quantity":1.5,"unit":"lb"},{"text":"0.25 Lb.","quantity":0.25,"unit":"lb"},{"text":"2 lbs","quantity":2,"unit":"lb"},{"text":"2lbs.","quantity":2,"unit":"lb"},{"text":"1,5 LBS","quantity":1.5,"unit":"lb"},{"text":"0.25 Lbs.","quantity":0.25,"unit":"lb"},{"text":"2 pound","quantity":2,"unit":"lb"},{"text":"2pound.","quantity":2,"unit":"lb"},{"text":"1,5 POUND","quantity":1.5,"unit":"lb"},{"text":"0.25 Pound.","quantity":0.25,"unit":"lb"},{"text":"2 pounds","quantity":2,"unit":"lb"},{"text":"2pounds.","quantity":2,"unit":"lb"},{"text":"1,5 POUNDS","quantity":1.5,"unit":"lb"},{"text":"0.25 Pounds.","quantity":0.25,"unit":"lb"},{"text":"2 ml","quantity":2,"unit":"ml"},{"text":"2ml.","quantity":2,"unit":"ml"},{"text":"1,5 ML","quantity":1.5,"unit":"ml"},{"text":"0.25 Ml.","quantity":0.25,"unit":"ml"},{"text":"2 milliliter","quantity":2,"unit":"ml"},{"text":"2milliliter.","quantity":2,"unit":"ml"},{"text":"1,5 MILLILITER","quantity":1.5,"unit":"ml"},{"text":"0.25 Milliliter.","quantity":0.25,"unit":"ml"},{"text":"2 milliliters","quantity":2,"unit":"ml"},{"text":"2milliliters.","quantity":2,"unit":"ml"},{"text":"1,5 MILLILITERS","quantity":1.5,"unit":"ml"},{"text":"0.25 Milliliters.","quantity":0.25,"unit":"ml"},{"text":"2 millilitre","quantity":2,"unit":"ml"},{"text":"2millilitre.","quantity":2,"unit":"ml"},{"text":"1,5 MILLILITRE","quantity":1.5,"unit":"ml"},{"text":"0.25 Millilitre.","quantity":0.25,"unit":"ml"},{"text":"2 millilitres","quantity":2,"unit":"ml"},{"text":"2millilitres.","quantity":2,"unit":"ml"},{"text":"1,5 MILLILITRES","quantity":1.5,"unit":"ml"},{"text":"0.25 Millilitres.","quantity":0.25,"unit":"ml"},{"text":"2 dl","quantity":2,"unit":"dl"},{"text":"2dl.","quantity":2,"unit":"dl"},{"text":"1,5 DL","quantity":1.5,"unit":"dl"},{"text":"0.25 Dl.","quantity":0.25,"unit":"dl"},{"text":"2 deciliter","quantity":2,"unit":"dl"},{"text":"2deciliter.","quantity":2,"unit":"dl"},{"text":"1,5 DECILITER","quantity":1.5,"unit":"dl"},{"text":"0.25 Deciliter.","quantity":0.25,"unit":"dl"},{"text":"2 deciliters","quantity":2,"unit":"dl"},{"text":"2deciliters.","quantity":2,"unit":"dl"},{"text":"1,5 DECILITERS","quantity":1.5,"unit":"dl"},{"text":"0.25 Deciliters.","quantity":0.25,"unit":"dl"},{"text":"2 decilitre","quantity":2,"unit":"dl"},{"text":"2decilitre.","quantity":2,"unit":"dl"},{"text":"1,5 DECILITRE","quantity":1.5,"unit":"dl"},{"text":"0.25 Decilitre.","quantity":0.25,"unit":"dl"},{"text":"2 decilitres","quantity":2,"unit":"dl"},{"text":"2decilitres.","quantity":2,"unit":"dl"},{"text":"1,5 DECILITRES","quantity":1.5,"unit":"dl"},{"text":"0.25 Decilitres.","quantity":0.25,"unit":"dl"},{"text":"2 l","quantity":2,"unit":"l"},{"text":"2l.","quantity":2,"unit":"l"},{"text":"1,5 L","quantity":1.5,"unit":"l"},{"text":"0.25 L.","quantity":0.25,"unit":"l"},{"text":"2 liter","quantity":2,"unit":"l"},{"text":"2liter.","quantity":2,"unit":"l"},{"text":"1,5 LITER","quantity":1.5,"unit":"l"},{"text":"0.25 Liter.","quantity":0.25,"unit":"l"},{"text":"2 liters","quantity":2,"unit":"l"},{"text":"2liters.","quantity":2,"unit":"l"},{"text":"1,5 LITERS","quantity":1.5,"unit":"l"},{"text":"0.25 Liters.","quantity":0.25,"unit":"l"},{"text":"2 litre","quantity":2,"unit":"l"},{"text":"2litre.","quantity":2,"unit":"l"},{"text":"1,5 LITRE","quantity":1.5,"unit":"l"},{"text":"0.25 Litre.","quantity":0.25,"unit":"l"},{"text":"2 litres","quantity":2,"unit":"l"},{"text":"2litres.","quantity":2,"unit":"l"},{"text":"1,5 LITRES","quantity":1.5,"unit":"l"},{"text":"0.25 Litres.","quantity":0.25,"unit":"l"},{"text":"2 cup","quantity":2,"unit":"cup"},{"text":"2cup.","quantity":2,"unit":"cup"},{"text":"1,5 CUP","quantity":1.5,"unit":"cup"},{"text":"0.25 Cup.","quantity":0.25,"unit":"cup"},{"text":"2 cups","quantity":2,"unit":"cup"},{"text":"2cups.","quantity":2,"unit":"cup"},{"text":"1,5 CUPS","quantity":1.5,"unit":"cup"},{"text":"0.25 Cups.","quantity":0.25,"unit":"cup"},{"text":"2 kop","quantity":2,"unit":"cup"},{"text":"2kop.","quantity":2,"unit":"cup"},{"text":"1,5 KOP","quantity":1.5,"unit":"cup"},{"text":"0.25 Kop.","quantity":0.25,"unit":"cup"},{"text":"2 kopper","quantity":2,"unit":"cup"},{"text":"2kopper.","quantity":2,"unit":"cup"},{"text":"1,5 KOPPER","quantity":1.5,"unit":"cup"},{"text":"0.25 Kopper.","quantity":0.25,"unit":"cup"},{"text":"2 tsk","quantity":2,"unit":"tsk"},{"text":"2tsk.","quantity":2,"unit":"tsk"},{"text":"1,5 TSK","quantity":1.5,"unit":"tsk"},{"text":"0.25 Tsk.","quantity":0.25,"unit":"tsk"},{"text":"2 teske","quantity":2,"unit":"tsk"},{"text":"2teske.","quantity":2,"unit":"tsk"},{"text":"1,5 TESKE","quantity":1.5,"unit":"tsk"},{"text":"0.25 Teske.","quantity":0.25,"unit":"tsk"},{"text":"2 teskeer","quantity":2,"unit":"tsk"},{"text":"2teskeer.","quantity":2,"unit":"tsk"},{"text":"1,5 TESKEER","quantity":1.5,"unit":"tsk"},{"text":"0.25 Teskeer.","quantity":0.25,"unit":"tsk"},{"text":"2 teskefuld","quantity":2,"unit":"tsk"},{"text":"2teskefuld.","quantity":2,"unit":"tsk"},{"text":"1,5 TESKEFULD","quantity":1.5,"unit":"tsk"},{"text":"0.25 Teskefuld.","quantity":0.25,"unit":"tsk"},{"text":"2 teskefulde","quantity":2,"unit":"tsk"},{"text":"2teskefulde.","quantity":2,"unit":"tsk"},{"text":"1,5 TESKEFULDE","quantity":1.5,"unit":"tsk"},{"text":"0.25 Teskefulde.","quantity":0.25,"unit":"tsk"},{"text":"2 tsp","quantity":2,"unit":"tsk"},{"text":"2tsp.","quantity":2,"unit":"tsk"},{"text":"1,5 TSP","quantity":1.5,"unit":"tsk"},{"text":"0.25 Tsp.","quantity":0.25,"unit":"tsk"},{"text":"2 tsps","quantity":2,"unit":"tsk"},{"text":"2tsps.","quantity":2,"unit":"tsk"},{"text":"1,5 TSPS","quantity":1.5,"unit":"tsk"},{"text":"0.25 Tsps.","quantity":0.25,"unit":"tsk"},{"text":"2 teaspoon","quantity":2,"unit":"tsk"},{"text":"2teaspoon.","quantity":2,"unit":"tsk"},{"text":"1,5 TEASPOON","quantity":1.5,"unit":"tsk"},{"text":"0.25 Teaspoon.","quantity":0.25,"unit":"tsk"},{"text":"2 teaspoons","quantity":2,"unit":"tsk"},{"text":"2teaspoons.","quantity":2,"unit":"tsk"},{"text":"1,5 TEASPOONS","quantity":1.5,"unit":"tsk"},{"text":"0.25 Teaspoons.","quantity":0.25,"unit":"tsk"},{"text":"2 spsk","quantity":2,"unit":"spsk"},{"text":"2spsk.","quantity":2,"unit":"spsk"},{"text":"1,5 SPSK","quantity":1.5,"unit":"spsk"},{"text":"0.25 Spsk.","quantity":0.25,"unit":"spsk"},{"text":"2 spiseske","quantity":2,"unit":"spsk"},{"text":"2spiseske.","quantity":2,"unit":"spsk"},{"text":"1,5 SPISESKE","quantity":1.5,"unit":"spsk"},{"text":"0.25 Spiseske.","quantity":0.25,"unit":"spsk"},{"text":"2 spiseskeer","quantity":2,"unit":"spsk"},{"text":"2spiseskeer.","quantity":2,"unit":"spsk"},{"text":"1,5 SPISESKEER","quantity":1.5,"unit":"spsk"},{"text":"0.25 Spiseskeer.","quantity":0.25,"unit":"spsk"},{"text":"2 spiseskefuld","quantity":2,"unit":"spsk"},{"text":"2spiseskefuld.","quantity":2,"unit":"spsk"},{"text":"1,5 SPISESKEFULD","quantity":1.5,"unit":"spsk"},{"text":"0.25 Spiseskefuld.","quantity":0.25,"unit":"spsk"},{"text":"2 spiseskefulde","quantity":2,"unit":"spsk"},{"text":"2spiseskefulde.","quantity":2,"unit":"spsk"},{"text":"1,5 SPISESKEFULDE","quantity":1.5,"unit":"spsk"},{"text":"0.25 Spiseskefulde.","quantity":0.25,"unit":"spsk"},{"text":"2 tbsp","quantity":2,"unit":"spsk"},{"text":"2tbsp.","quantity":2,"unit":"spsk"},{"text":"1,5 TBSP","quantity":1.5,"unit":"spsk"},{"text":"0.25 Tbsp.","quantity":0.25,"unit":"spsk"},{"text":"2 tbsps","quantity":2,"unit":"spsk"},{"text":"2tbsps.","quantity":2,"unit":"spsk"},{"text":"1,5 TBSPS","quantity":1.5,"unit":"spsk"},{"text":"0.25 Tbsps.","quantity":0.25,"unit":"spsk"},{"text":"2 tbs","quantity":2,"unit":"spsk"},{"text":"2tbs.","quantity":2,"unit":"spsk"},{"text":"1,5 TBS","quantity":1.5,"unit":"spsk"},{"text":"0.25 Tbs.","quantity":0.25,"unit":"spsk"},{"text":"2 tablespoon","quantity":2,"unit":"spsk"},{"text":"2tablespoon.","quantity":2,"unit":"spsk"},{"text":"1,5 TABLESPOON","quantity":1.5,"unit":"spsk"},{"text":"0.25 Tablespoon.","quantity":0.25,"unit":"spsk"},{"text":"2 tablespoons","quantity":2,"unit":"spsk"},{"text":"2tablespoons.","quantity":2,"unit":"spsk"},{"text":"1,5 TABLESPOONS","quantity":1.5,"unit":"spsk"},{"text":"0.25 Tablespoons.","quantity":0.25,"unit":"spsk"},{"text":"2 stk","quantity":2,"unit":null},{"text":"2stk.","quantity":2,"unit":null},{"text":"1,5 STK","quantity":1.5,"unit":null},{"text":"0.25 Stk.","quantity":0.25,"unit":null},{"text":"2 styk","quantity":2,"unit":null},{"text":"2styk.","quantity":2,"unit":null},{"text":"1,5 STYK","quantity":1.5,"unit":null},{"text":"0.25 Styk.","quantity":0.25,"unit":null},{"text":"2 styks","quantity":2,"unit":null},{"text":"2styks.","quantity":2,"unit":null},{"text":"1,5 STYKS","quantity":1.5,"unit":null},{"text":"0.25 Styks.","quantity":0.25,"unit":null},{"text":"2 pc","quantity":2,"unit":null},{"text":"2pc.","quantity":2,"unit":null},{"text":"1,5 PC","quantity":1.5,"unit":null},{"text":"0.25 Pc.","quantity":0.25,"unit":null},{"text":"2 pcs","quantity":2,"unit":null},{"text":"2pcs.","quantity":2,"unit":null},{"text":"1,5 PCS","quantity":1.5,"unit":null},{"text":"0.25 Pcs.","quantity":0.25,"unit":null},{"text":"2 piece","quantity":2,"unit":null},{"text":"2piece.","quantity":2,"unit":null},{"text":"1,5 PIECE","quantity":1.5,"unit":null},{"text":"0.25 Piece.","quantity":0.25,"unit":null},{"text":"2 pieces","quantity":2,"unit":null},{"text":"2pieces.","quantity":2,"unit":null},{"text":"1,5 PIECES","quantity":1.5,"unit":null},{"text":"0.25 Pieces.","quantity":0.25,"unit":null},{"text":"2","quantity":2,"unit":null},{"text":"200","quantity":200,"unit":null},{"text":"999999","quantity":999999,"unit":null},{"text":"1,5","quantity":1.5,"unit":null},{"text":"1.5","quantity":1.5,"unit":null},{"text":"0,25","quantity":0.25,"unit":null},{"text":"0.5","quantity":0.5,"unit":null},{"text":"10.05","quantity":10.05,"unit":null},{"text":"200 g","quantity":200,"unit":"g"},{"text":"200g","quantity":200,"unit":"g"},{"text":"200\tg","quantity":200,"unit":"g"},{"text":"  200 g  ","quantity":200,"unit":"g"},{"text":"\t200 g\r\n","quantity":200,"unit":"g"},{"text":"200   g","quantity":200,"unit":"g"},{"text":"1,5 dl","quantity":1.5,"unit":"dl"},{"text":"1.5 dl","quantity":1.5,"unit":"dl"},{"text":"0,5 kg","quantity":0.5,"unit":"kg"},{"text":"2 stk.","quantity":2,"unit":null},{"text":"2 STK","quantity":2,"unit":null},{"text":"1/2","quantity":0.5,"unit":null},{"text":"1/2 tsk","quantity":0.5,"unit":"tsk"},{"text":"3/4 cup","quantity":0.75,"unit":"cup"},{"text":"1/3","quantity":0.333,"unit":null},{"text":"2/3 dl","quantity":0.667,"unit":"dl"},{"text":"5/4 l","quantity":1.25,"unit":"l"},{"text":"1/8","quantity":0.125,"unit":null},{"text":"1/16 tsp","quantity":0.063,"unit":"tsk"},{"text":"3/8","quantity":0.375,"unit":null},{"text":"2 1/2","quantity":2.5,"unit":null},{"text":"2 1/2 dl","quantity":2.5,"unit":"dl"},{"text":"1 3/4 cups","quantity":1.75,"unit":"cup"},{"text":"1\t1/3 kg","quantity":1.333,"unit":"kg"},{"text":"½","quantity":0.5,"unit":null},{"text":"½ dl","quantity":0.5,"unit":"dl"},{"text":"2½","quantity":2.5,"unit":null},{"text":"2½ dl","quantity":2.5,"unit":"dl"},{"text":"2 ½","quantity":2.5,"unit":null},{"text":"2 ½ spsk","quantity":2.5,"unit":"spsk"},{"text":"¼","quantity":0.25,"unit":null},{"text":"¼ dl","quantity":0.25,"unit":"dl"},{"text":"2¼","quantity":2.25,"unit":null},{"text":"2¼ dl","quantity":2.25,"unit":"dl"},{"text":"2 ¼","quantity":2.25,"unit":null},{"text":"2 ¼ spsk","quantity":2.25,"unit":"spsk"},{"text":"¾","quantity":0.75,"unit":null},{"text":"¾ dl","quantity":0.75,"unit":"dl"},{"text":"2¾","quantity":2.75,"unit":null},{"text":"2¾ dl","quantity":2.75,"unit":"dl"},{"text":"2 ¾","quantity":2.75,"unit":null},{"text":"2 ¾ spsk","quantity":2.75,"unit":"spsk"},{"text":"⅓","quantity":0.333,"unit":null},{"text":"⅓ dl","quantity":0.333,"unit":"dl"},{"text":"2⅓","quantity":2.333,"unit":null},{"text":"2⅓ dl","quantity":2.333,"unit":"dl"},{"text":"2 ⅓","quantity":2.333,"unit":null},{"text":"2 ⅓ spsk","quantity":2.333,"unit":"spsk"},{"text":"⅔","quantity":0.667,"unit":null},{"text":"⅔ dl","quantity":0.667,"unit":"dl"},{"text":"2⅔","quantity":2.667,"unit":null},{"text":"2⅔ dl","quantity":2.667,"unit":"dl"},{"text":"2 ⅔","quantity":2.667,"unit":null},{"text":"2 ⅔ spsk","quantity":2.667,"unit":"spsk"},{"text":"","quantity":null,"unit":null},{"text":"   ","quantity":null,"unit":null},{"text":"\t\r\n","quantity":null,"unit":null},{"text":"a pinch","quantity":null,"unit":null},{"text":"to taste","quantity":null,"unit":null},{"text":"salt","quantity":null,"unit":null},{"text":"1-2","quantity":null,"unit":null},{"text":"1-2 fed","quantity":null,"unit":null},{"text":"2 fed","quantity":null,"unit":null},{"text":"3 cloves","quantity":null,"unit":null},{"text":"1 dåse","quantity":null,"unit":null},{"text":"1 bundt","quantity":null,"unit":null},{"text":"2 head","quantity":null,"unit":null},{"text":"2 æg","quantity":null,"unit":null},{"text":"1 knivspids","quantity":null,"unit":null},{"text":"1.500 g","quantity":null,"unit":null},{"text":"1,500","quantity":null,"unit":null},{"text":"1.234 kg","quantity":null,"unit":null},{"text":"0","quantity":null,"unit":null},{"text":"0 g","quantity":null,"unit":null},{"text":"0,0","quantity":null,"unit":null},{"text":"0/5","quantity":null,"unit":null},{"text":"1/0","quantity":null,"unit":null},{"text":"1/0 g","quantity":null,"unit":null},{"text":"0,00 dl","quantity":null,"unit":null},{"text":"00000","quantity":null,"unit":null},{"text":"1234567","quantity":null,"unit":null},{"text":"1234567 g","quantity":null,"unit":null},{"text":"12345678","quantity":null,"unit":null},{"text":"g","quantity":null,"unit":null},{"text":"g 200","quantity":null,"unit":null},{"text":"dl 2","quantity":null,"unit":null},{"text":"½½","quantity":null,"unit":null},{"text":"½ ½","quantity":null,"unit":null},{"text":"1 ½ ½","quantity":null,"unit":null},{"text":"2 1/2 1/2","quantity":null,"unit":null},{"text":"1,5,5 g","quantity":null,"unit":null},{"text":"1..5 g","quantity":null,"unit":null},{"text":"1.5.","quantity":null,"unit":null},{"text":"2.","quantity":null,"unit":null},{"text":"2. g","quantity":null,"unit":null},{"text":"200 g g","quantity":null,"unit":null},{"text":"200 g extra","quantity":null,"unit":null},{"text":"200 ger","quantity":null,"unit":null},{"text":"200 gram.s","quantity":null,"unit":null},{"text":"-2 g","quantity":null,"unit":null},{"text":"+2 g","quantity":null,"unit":null},{"text":"2 - g","quantity":null,"unit":null},{"text":"1/2/3","quantity":null,"unit":null},{"text":"1 /2","quantity":null,"unit":null},{"text":"1/ 2","quantity":null,"unit":null},{"text":"1 / 2","quantity":null,"unit":null},{"text":"2 1 /2","quantity":null,"unit":null},{"text":"one egg","quantity":null,"unit":null},{"text":"1 2","quantity":null,"unit":null},{"text":"1 2 g","quantity":null,"unit":null},{"text":"1/1000000","quantity":null,"unit":null},{"text":"1/10000","quantity":null,"unit":null},{"text":"½ g½","quantity":null,"unit":null},{"text":"2,5½","quantity":null,"unit":null},{"text":"2.5 ½","quantity":null,"unit":null},{"text":"2 ½ ½","quantity":null,"unit":null},{"text":"٣ g","quantity":null,"unit":null},{"text":"２ g","quantity":null,"unit":null},{"text":"2 gé","quantity":null,"unit":null},{"text":"2 g1","quantity":null,"unit":null},{"text":"2 g 1","quantity":null,"unit":null},{"text":"2 kg-","quantity":null,"unit":null},{"text":"200 g.","quantity":200,"unit":"g"},{"text":"2 g.","quantity":2,"unit":"g"}]}$contract$::jsonb -> 'cases') as c(text text, quantity numeric, unit text);
-- contract:end

select cmp_ok((select count(*) from contract), '>', 300::bigint, 'the contract has its cases');

select is_empty(
    $$ select c.text from contract c
       where not exists (
           select 1 from public.parse_amount(c.text) p
           where p.quantity is not distinct from c.quantity and p.unit is not distinct from c.unit) $$,
    'parse_amount reads every contract case as the contract says'
);

-- Fixtures, inserted as postgres (the table owner, so RLS doesn't apply).
insert into auth.users (id, email, aud, role) values
    ('aaaaaaaa-0000-0000-0000-0000000000a1', 'user-a@rls.test', 'authenticated', 'authenticated');

insert into public.meals (id, name, meal_type, created_by, created_at) values
    (920001, 'A pasta', 'regular', 'aaaaaaaa-0000-0000-0000-0000000000a1', now());

insert into public.meal_plans (id, start_date, end_date, created_by, created_at) values
    (920001, '2026-01-05', '2026-01-11', 'aaaaaaaa-0000-0000-0000-0000000000a1', now());

insert into public."meal_plans-meals" (id, meal_date, fk_meal, fk_meal_plan, created_by, created_at) values
    (920001, '2026-01-05', 920001, 920001, 'aaaaaaaa-0000-0000-0000-0000000000a1', now());

-- The checks, on each of the three tables: every code is accepted; a bogus unit, a quantity of 0 and a unit without a
-- quantity are rejected.
select lives_ok(
    $$ insert into public.ingredients (name, amount, amount_quantity, amount_unit, fk_meal, created_by, created_at)
       select 'x', '1 ' || u, 1, u, 920001, 'aaaaaaaa-0000-0000-0000-0000000000a1', now()
       from unnest(array['g', 'kg', 'oz', 'lb', 'ml', 'dl', 'l', 'cup', 'tsk', 'spsk']) as u $$,
    'ingredients accept every unit code'
);
select throws_ok(
    $$ insert into public.ingredients (name, amount, amount_quantity, amount_unit, fk_meal, created_by, created_at)
       values ('x', '1 stk', 1, 'stk', 920001, 'aaaaaaaa-0000-0000-0000-0000000000a1', now()) $$,
    '23514', null, 'ingredients reject a unit that is not on the list'
);
select throws_ok(
    $$ insert into public.ingredients (name, amount, amount_quantity, amount_unit, fk_meal, created_by, created_at)
       values ('x', '0 g', 0, 'g', 920001, 'aaaaaaaa-0000-0000-0000-0000000000a1', now()) $$,
    '23514', null, 'ingredients reject a quantity of 0'
);
select throws_ok(
    $$ insert into public.ingredients (name, amount, amount_quantity, amount_unit, fk_meal, created_by, created_at)
       values ('x', 'g', null, 'g', 920001, 'aaaaaaaa-0000-0000-0000-0000000000a1', now()) $$,
    '23514', null, 'ingredients reject a unit without a quantity'
);

select lives_ok(
    $$ insert into public.grocery_items (name, amount, amount_quantity, amount_unit, is_picked, is_manual, created_by, created_at)
       select 'x', '1 ' || u, 1, u, false, true, 'aaaaaaaa-0000-0000-0000-0000000000a1', now()
       from unnest(array['g', 'kg', 'oz', 'lb', 'ml', 'dl', 'l', 'cup', 'tsk', 'spsk']) as u $$,
    'grocery items accept every unit code'
);
select throws_ok(
    $$ insert into public.grocery_items (name, amount, amount_quantity, amount_unit, is_picked, is_manual, created_by, created_at)
       values ('x', '1 stk', 1, 'stk', false, true, 'aaaaaaaa-0000-0000-0000-0000000000a1', now()) $$,
    '23514', null, 'grocery items reject a unit that is not on the list'
);
select throws_ok(
    $$ insert into public.grocery_items (name, amount, amount_quantity, amount_unit, is_picked, is_manual, created_by, created_at)
       values ('x', '0 g', 0, 'g', false, true, 'aaaaaaaa-0000-0000-0000-0000000000a1', now()) $$,
    '23514', null, 'grocery items reject a quantity of 0'
);
select throws_ok(
    $$ insert into public.grocery_items (name, amount, amount_quantity, amount_unit, is_picked, is_manual, created_by, created_at)
       values ('x', 'g', null, 'g', false, true, 'aaaaaaaa-0000-0000-0000-0000000000a1', now()) $$,
    '23514', null, 'grocery items reject a unit without a quantity'
);

insert into public.grocery_items (id, name, amount, is_picked, is_manual, created_by, created_at) values
    (920001, 'A flour', '200 g', false, false, 'aaaaaaaa-0000-0000-0000-0000000000a1', now());
insert into public.ingredients (id, name, amount, fk_meal, created_by, created_at) values
    (920001, 'A flour', '200 g', 920001, 'aaaaaaaa-0000-0000-0000-0000000000a1', now());

select lives_ok(
    $$ insert into public.grocery_item_sources (fk_grocery_item, fk_planned_meal, fk_ingredient, amount, amount_quantity, amount_unit, created_by)
       values (920001, 920001, 920001, '1 kg', 1, 'kg', 'aaaaaaaa-0000-0000-0000-0000000000a1') $$,
    'links accept a unit code'
);
select throws_ok(
    $$ insert into public.grocery_item_sources (fk_grocery_item, fk_planned_meal, fk_ingredient, amount, amount_quantity, amount_unit, created_by)
       values (920001, 920001, 920001, '1 stk', 1, 'stk', 'aaaaaaaa-0000-0000-0000-0000000000a1') $$,
    '23514', null, 'links reject a unit that is not on the list'
);
select throws_ok(
    $$ insert into public.grocery_item_sources (fk_grocery_item, fk_planned_meal, fk_ingredient, amount, amount_quantity, amount_unit, created_by)
       values (920001, 920001, 920001, '0 g', 0, 'g', 'aaaaaaaa-0000-0000-0000-0000000000a1') $$,
    '23514', null, 'links reject a quantity of 0'
);
select throws_ok(
    $$ insert into public.grocery_item_sources (fk_grocery_item, fk_planned_meal, fk_ingredient, amount, amount_quantity, amount_unit, created_by)
       values (920001, 920001, 920001, 'g', null, 'g', 'aaaaaaaa-0000-0000-0000-0000000000a1') $$,
    '23514', null, 'links reject a unit without a quantity'
);

-- Nobody calls the parser through the API.
set local role anon;
select set_config('request.jwt.claims', '{"role":"anon"}', true);
select throws_ok($$ select * from public.parse_amount('200 g') $$, '42501', null, 'anon cannot call parse_amount');

reset role;
set local role authenticated;
select set_config('request.jwt.claims', '{"sub":"aaaaaaaa-0000-0000-0000-0000000000a1","role":"authenticated"}', true);
select throws_ok($$ select * from public.parse_amount('200 g') $$, '42501', null, 'authenticated users cannot call parse_amount');
reset role;

-- The backfill: rows that only have their text (as every row did before the migration).
insert into public.ingredients (id, name, amount, fk_meal, created_by, created_at, updated_at) values
    (920101, 'flour',  '200 g',   920001, 'aaaaaaaa-0000-0000-0000-0000000000a1', now(), '2026-01-01'),
    (920102, 'milk',   '1,5 dl',  920001, 'aaaaaaaa-0000-0000-0000-0000000000a1', now(), '2026-01-01'),
    (920103, 'eggs',   '2',       920001, 'aaaaaaaa-0000-0000-0000-0000000000a1', now(), '2026-01-01'),
    (920104, 'salt',   'a pinch', 920001, 'aaaaaaaa-0000-0000-0000-0000000000a1', now(), '2026-01-01'),
    (920105, 'garlic', '2 fed',   920001, 'aaaaaaaa-0000-0000-0000-0000000000a1', now(), '2026-01-01');
insert into public.grocery_items (id, name, amount, is_picked, is_manual, created_by, created_at, updated_at) values
    (920102, 'flour', '2½ tbsp',  false, false, 'aaaaaaaa-0000-0000-0000-0000000000a1', now(), '2026-01-01'),
    (920103, 'salt',  'to taste', false, false, 'aaaaaaaa-0000-0000-0000-0000000000a1', now(), '2026-01-01');
insert into public.grocery_item_sources (id, fk_grocery_item, fk_planned_meal, fk_ingredient, amount, created_by) values
    (920102, 920102, 920001, 920102, '1/2 cup', 'aaaaaaaa-0000-0000-0000-0000000000a1'),
    (920103, 920103, 920001, 920104, '1-2',     'aaaaaaaa-0000-0000-0000-0000000000a1');

-- The migration's backfill statements, verbatim.
update public.ingredients t
set (amount_quantity, amount_unit) = (select p.quantity, p.unit from public.parse_amount(t.amount) p);

update public.grocery_items t
set (amount_quantity, amount_unit) = (select p.quantity, p.unit from public.parse_amount(t.amount) p);

update public.grocery_item_sources t
set (amount_quantity, amount_unit) = (select p.quantity, p.unit from public.parse_amount(t.amount) p);

select results_eq(
    $$ select id, amount_quantity, amount_unit from public.ingredients where id between 920101 and 920105 order by id $$,
    $$ values (920101::bigint, 200::numeric, 'g'::text),
              (920102::bigint, 1.5::numeric, 'dl'::text),
              (920103::bigint, 2::numeric, null::text),
              (920104::bigint, null::numeric, null::text),
              (920105::bigint, null::numeric, null::text) $$,
    'ingredients get a quantity and unit where the text can be read, and null where it cannot'
);
select results_eq(
    $$ select id, amount_quantity, amount_unit from public.grocery_items where id in (920102, 920103) order by id $$,
    $$ values (920102::bigint, 2.5::numeric, 'spsk'::text), (920103::bigint, null::numeric, null::text) $$,
    'grocery items get the same'
);
select results_eq(
    $$ select id, amount_quantity, amount_unit from public.grocery_item_sources where id in (920102, 920103) order by id $$,
    $$ values (920102::bigint, 0.5::numeric, 'cup'::text), (920103::bigint, null::numeric, null::text) $$,
    'links get the same'
);
select results_eq(
    $$ select (select string_agg(amount, '|' order by id) from public.ingredients where id between 920101 and 920105),
              (select string_agg(amount, '|' order by id) from public.grocery_items where id in (920102, 920103)),
              (select string_agg(amount, '|' order by id) from public.grocery_item_sources where id in (920102, 920103)) $$,
    $$ values ('200 g|1,5 dl|2|a pinch|2 fed'::text, '2½ tbsp|to taste'::text, '1/2 cup|1-2'::text) $$,
    'the text of every row is exactly as it was'
);
select is(
    (select count(*) from public.ingredients where id between 920101 and 920105 and updated_at <> '2026-01-01'),
    0::bigint,
    'and updated_at is untouched'
);

select * from finish();
rollback;
