-- Amounts as a quantity and a unit.
--
-- The amount stays free text, as typed. Next to it, ingredients, grocery items and grocery item links get
-- amount_quantity and amount_unit, filled by the client from the text ("200 g" -> 200, 'g'; "2" -> 2, null for a
-- count; "a pinch" -> null, null). A null quantity means the text couldn't be read.
--
-- public.parse_amount below reads the text with the same rules as the client's ParsedAmount (C#). Both are pinned to
-- the contract MealWiz/tests/Contracts/amount_parsing.json (see supabase/tests/database/amounts.test.sql). It is used
-- here to convert the existing rows, and nobody may call it through the API: the client does its own reading.

-- Rules (keep in step with ParsedAmount): trim space, tab, CR and LF; a quantity is an integer of 1-6 digits, a decimal
-- with "," or "." and 1-2 decimals, a fraction a/b, a mixed "2 1/2", or an optional integer plus one of ½ ¼ ¾ ⅓ ⅔;
-- rounded to 3 decimals (half away from zero) and above 0; then an optional unit word of letters with an optional ".".
-- Anything else is unreadable: null, null. ASCII classes only.
create function public.parse_amount(amount text)
returns table(quantity numeric, unit text)
language plpgsql
immutable
security invoker
set search_path = ''
as $$
declare
    -- Tabs count as spaces from here on; the unit comes last: spaces, then an optional word and ".".
    tail constant text := '[ ]*(?:([A-Za-z]+)[.]?)?$';
    t text;
    m text[];
    q numeric;
    word text;
    u text;
begin
    if amount is null then
        return query select null::numeric, null::text;
        return;
    end if;

    t := translate(btrim(amount, E' \t\r\n'), E'\t', ' ');

    m := regexp_match(t, '^([0-9]{1,6})[ ]+([0-9]{1,6})/([0-9]{1,6})' || tail);
    if m is not null then
        if m[3]::numeric = 0 then
            return query select null::numeric, null::text;
            return;
        end if;
        q := m[1]::numeric + m[2]::numeric / m[3]::numeric;
        word := m[4];
    else
        m := regexp_match(t, '^([0-9]{1,6})/([0-9]{1,6})' || tail);
        if m is not null then
            if m[2]::numeric = 0 then
                return query select null::numeric, null::text;
                return;
            end if;
            q := m[1]::numeric / m[2]::numeric;
            word := m[3];
        else
            m := regexp_match(t, '^([0-9]{1,6})?[ ]?([½¼¾⅓⅔])' || tail);
            if m is not null then
                q := coalesce(m[1]::numeric, 0) + case m[2]
                    when '½' then 0.5
                    when '¼' then 0.25
                    when '¾' then 0.75
                    when '⅓' then 1::numeric / 3
                    else 2::numeric / 3
                end;
                word := m[3];
            else
                m := regexp_match(t, '^([0-9]{1,6}(?:[.,][0-9]{1,2})?)' || tail);
                if m is null then
                    return query select null::numeric, null::text;
                    return;
                end if;
                q := replace(m[1], ',', '.')::numeric;
                word := m[2];
            end if;
        end if;
    end if;

    q := round(q, 3);
    if q <= 0 then
        return query select null::numeric, null::text;
        return;
    end if;

    if word is null then
        u := null;
    else
        u := case lower(word)
            when 'g' then 'g' when 'gr' then 'g' when 'gram' then 'g' when 'grams' then 'g'
            when 'kg' then 'kg' when 'kilo' then 'kg' when 'kilos' then 'kg' when 'kilogram' then 'kg' when 'kilograms' then 'kg'
            when 'oz' then 'oz' when 'ounce' then 'oz' when 'ounces' then 'oz'
            when 'lb' then 'lb' when 'lbs' then 'lb' when 'pound' then 'lb' when 'pounds' then 'lb'
            when 'ml' then 'ml' when 'milliliter' then 'ml' when 'milliliters' then 'ml'
            when 'millilitre' then 'ml' when 'millilitres' then 'ml'
            when 'dl' then 'dl' when 'deciliter' then 'dl' when 'deciliters' then 'dl'
            when 'decilitre' then 'dl' when 'decilitres' then 'dl'
            when 'l' then 'l' when 'liter' then 'l' when 'liters' then 'l' when 'litre' then 'l' when 'litres' then 'l'
            when 'cup' then 'cup' when 'cups' then 'cup' when 'kop' then 'cup' when 'kopper' then 'cup'
            when 'tsk' then 'tsk' when 'teske' then 'tsk' when 'teskeer' then 'tsk' when 'teskefuld' then 'tsk'
            when 'teskefulde' then 'tsk' when 'tsp' then 'tsk' when 'tsps' then 'tsk'
            when 'teaspoon' then 'tsk' when 'teaspoons' then 'tsk'
            when 'spsk' then 'spsk' when 'spiseske' then 'spsk' when 'spiseskeer' then 'spsk'
            when 'spiseskefuld' then 'spsk' when 'spiseskefulde' then 'spsk'
            when 'tbsp' then 'spsk' when 'tbsps' then 'spsk' when 'tbs' then 'spsk'
            when 'tablespoon' then 'spsk' when 'tablespoons' then 'spsk'
            when 'stk' then 'count' when 'styk' then 'count' when 'styks' then 'count'
            when 'pc' then 'count' when 'pcs' then 'count' when 'piece' then 'count' when 'pieces' then 'count'
        end;
        if u is null then
            return query select null::numeric, null::text;
            return;
        end if;
        if u = 'count' then
            u := null;
        end if;
    end if;

    return query select q, u;
end;
$$;

-- Supabase grants execute on new public functions to anon and authenticated by default.
revoke all on function public.parse_amount(text) from public, anon, authenticated;

-- The columns and their checks.
alter table public.ingredients
    add column amount_quantity numeric,
    add column amount_unit text,
    add constraint ingredients_amount_unit_check
        check (amount_unit in ('g', 'kg', 'oz', 'lb', 'ml', 'dl', 'l', 'cup', 'tsk', 'spsk')),
    add constraint ingredients_amount_quantity_check check (amount_quantity > 0),
    add constraint ingredients_amount_unit_needs_quantity_check
        check (amount_unit is null or amount_quantity is not null);

alter table public.grocery_items
    add column amount_quantity numeric,
    add column amount_unit text,
    add constraint grocery_items_amount_unit_check
        check (amount_unit in ('g', 'kg', 'oz', 'lb', 'ml', 'dl', 'l', 'cup', 'tsk', 'spsk')),
    add constraint grocery_items_amount_quantity_check check (amount_quantity > 0),
    add constraint grocery_items_amount_unit_needs_quantity_check
        check (amount_unit is null or amount_quantity is not null);

alter table public.grocery_item_sources
    add column amount_quantity numeric,
    add column amount_unit text,
    add constraint grocery_item_sources_amount_unit_check
        check (amount_unit in ('g', 'kg', 'oz', 'lb', 'ml', 'dl', 'l', 'cup', 'tsk', 'spsk')),
    add constraint grocery_item_sources_amount_quantity_check check (amount_quantity > 0),
    add constraint grocery_item_sources_amount_unit_needs_quantity_check
        check (amount_unit is null or amount_quantity is not null);

-- Backfill: existing rows get a quantity and unit where the text can be read. The text and updated_at are untouched.
-- (seed.sql repeats these three statements for the seeded rows, which are inserted after the migrations.)
update public.ingredients t
set (amount_quantity, amount_unit) = (select p.quantity, p.unit from public.parse_amount(t.amount) p);

update public.grocery_items t
set (amount_quantity, amount_unit) = (select p.quantity, p.unit from public.parse_amount(t.amount) p);

update public.grocery_item_sources t
set (amount_quantity, amount_unit) = (select p.quantity, p.unit from public.parse_amount(t.amount) p);

-- The write functions of add_atomic_write_functions, now also writing the two columns. Same signatures (so grants
-- stay). Every jsonb element that has "amount" also carries "amount_quantity" and "amount_unit"; a missing key is null.

-- UpdateLinkedGroceryAmounts: set each to-buy item's amount, then each link's remembered amount.
-- item_amounts and source_amounts: [{"id": 1, "amount": "200 g", "amount_quantity": 200, "amount_unit": "g"}, ...]
create or replace function public.update_linked_grocery_amounts(item_amounts jsonb, source_amounts jsonb)
returns void
language plpgsql
security invoker
set search_path = ''
as $$
declare
    changed integer;
begin
    update public.grocery_items g
    set amount = a.amount, amount_quantity = a.amount_quantity, amount_unit = a.amount_unit, updated_at = now()
    from jsonb_to_recordset(item_amounts) as a(id bigint, amount text, amount_quantity numeric, amount_unit text)
    where g.id = a.id and g.is_picked = false;
    get diagnostics changed = row_count;
    if changed <> jsonb_array_length(item_amounts) then
        raise exception 'grocery_item_not_found' using errcode = 'P0002';
    end if;

    update public.grocery_item_sources s
    set amount = a.amount, amount_quantity = a.amount_quantity, amount_unit = a.amount_unit
    from jsonb_to_recordset(source_amounts) as a(id bigint, amount text, amount_quantity numeric, amount_unit text)
    where s.id = a.id;
    get diagnostics changed = row_count;
    if changed <> jsonb_array_length(source_amounts) then
        raise exception 'grocery_item_source_not_found' using errcode = 'P0002';
    end if;
end;
$$;

-- DeleteGroceryItemSources: drop the links, then lower the amounts of the items that still stay on the list.
-- item_amounts: [{"id": 1, "amount": "100 g", "amount_quantity": 100, "amount_unit": "g"}, ...]
create or replace function public.drop_grocery_item_sources(source_ids bigint[], item_amounts jsonb)
returns void
language plpgsql
security invoker
set search_path = ''
as $$
declare
    changed integer;
begin
    delete from public.grocery_item_sources where id = any(source_ids);

    update public.grocery_items g
    set amount = a.amount, amount_quantity = a.amount_quantity, amount_unit = a.amount_unit, updated_at = now()
    from jsonb_to_recordset(item_amounts) as a(id bigint, amount text, amount_quantity numeric, amount_unit text)
    where g.id = a.id;
    get diagnostics changed = row_count;
    if changed <> jsonb_array_length(item_amounts) then
        raise exception 'grocery_item_not_found' using errcode = 'P0002';
    end if;
end;
$$;

-- AddPlanIngredientsToGroceryList: update the amounts of existing items, insert the new items, and link every entry to
-- its item.
-- existing_items: [{"id": 1, "amount": "300 g", "amount_quantity": 300, "amount_unit": "g"}, ...]
-- new_items:      [{"key": 0, "name": "Flour", "amount": "200 g", "amount_quantity": 200, "amount_unit": "g"}, ...]
--                 (key: the handler's number for the new item)
-- sources:        [{"item_id": 1, "new_item_key": null, "planned_meal_id": 5, "ingredient_id": 9, "amount": "100 g",
--                   "amount_quantity": 100, "amount_unit": "g"}, ...]
--                 (item_id for an existing item, new_item_key for one of new_items)
-- Returns {"updated": [ids of existing_items], "inserted": [ids of new_items, in key order]}.
create or replace function public.add_plan_ingredients_to_grocery_list(existing_items jsonb, new_items jsonb, sources jsonb)
returns jsonb
language plpgsql
security invoker
set search_path = ''
as $$
declare
    changed integer;
    new_item record;
    new_id bigint;
    id_of_key jsonb := '{}'::jsonb;
    inserted_ids bigint[] := '{}';
    linked integer;
begin
    update public.grocery_items g
    set amount = e.amount, amount_quantity = e.amount_quantity, amount_unit = e.amount_unit, updated_at = now()
    from jsonb_to_recordset(existing_items) as e(id bigint, amount text, amount_quantity numeric, amount_unit text)
    where g.id = e.id;
    get diagnostics changed = row_count;
    if changed <> jsonb_array_length(existing_items) then
        raise exception 'grocery_item_not_found' using errcode = 'P0002';
    end if;

    for new_item in
        select n.key, n.name, n.amount, n.amount_quantity, n.amount_unit
        from jsonb_to_recordset(new_items)
            as n(key integer, name text, amount text, amount_quantity numeric, amount_unit text)
        order by n.key
    loop
        insert into public.grocery_items (name, amount, amount_quantity, amount_unit, is_picked, is_manual, created_by, created_at)
        values (new_item.name, new_item.amount, new_item.amount_quantity, new_item.amount_unit, false, false, (select auth.uid()), now())
        returning id into new_id;

        id_of_key := id_of_key || jsonb_build_object(new_item.key::text, new_id);
        inserted_ids := inserted_ids || new_id;
    end loop;

    insert into public.grocery_item_sources
        (fk_grocery_item, fk_planned_meal, fk_ingredient, amount, amount_quantity, amount_unit, created_by, created_at)
    select coalesce(s.item_id, (id_of_key ->> s.new_item_key::text)::bigint),
           s.planned_meal_id, s.ingredient_id, s.amount, s.amount_quantity, s.amount_unit, (select auth.uid()), now()
    from jsonb_to_recordset(sources)
        as s(item_id bigint, new_item_key integer, planned_meal_id bigint, ingredient_id bigint, amount text,
             amount_quantity numeric, amount_unit text);
    get diagnostics linked = row_count;
    if linked <> jsonb_array_length(sources) then
        raise exception 'grocery_item_source_not_added' using errcode = 'P0002';
    end if;

    return jsonb_build_object(
        'updated', coalesce((select jsonb_agg(e.id) from jsonb_to_recordset(existing_items) as e(id bigint)), '[]'::jsonb),
        'inserted', to_jsonb(inserted_ids));
end;
$$;
