-- Multi-step writes in one transaction.
--
-- The client used to make these changes as several requests in a row, so a dropped connection or a failed step left
-- half of them applied (an item without its link, a link dropped without the amount lowered, half a week saved).
-- Each function below does one handler's writes as a single call, and therefore in a single transaction: all of it
-- happens or none of it does.
--
-- They are SECURITY INVOKER: they run as the calling user, so the tables' RLS policies still decide what the user may
-- read and write, exactly as for the separate requests before. Only signed-in users may call them. The handlers keep
-- their reading, checking and amount arithmetic; these functions only write. A write that matches fewer rows than it
-- was given raises an error the handler turns into its usual message, and the whole call rolls back.

-- UnlinkGroceryItems: drop the items' links and mark them manual.
create function public.unlink_grocery_items(item_ids bigint[])
returns void
language plpgsql
security invoker
set search_path = ''
as $$
begin
    delete from public.grocery_item_sources where fk_grocery_item = any(item_ids);

    update public.grocery_items
    set is_manual = true, updated_at = now()
    where id = any(item_ids);
end;
$$;

-- UpdateLinkedGroceryAmounts: set each to-buy item's amount, then each link's remembered amount.
-- item_amounts and source_amounts: [{"id": 1, "amount": "200 g"}, ...]
create function public.update_linked_grocery_amounts(item_amounts jsonb, source_amounts jsonb)
returns void
language plpgsql
security invoker
set search_path = ''
as $$
declare
    changed integer;
begin
    update public.grocery_items g
    set amount = a.amount, updated_at = now()
    from jsonb_to_recordset(item_amounts) as a(id bigint, amount text)
    where g.id = a.id and g.is_picked = false;
    get diagnostics changed = row_count;
    if changed <> jsonb_array_length(item_amounts) then
        raise exception 'grocery_item_not_found' using errcode = 'P0002';
    end if;

    update public.grocery_item_sources s
    set amount = a.amount
    from jsonb_to_recordset(source_amounts) as a(id bigint, amount text)
    where s.id = a.id;
    get diagnostics changed = row_count;
    if changed <> jsonb_array_length(source_amounts) then
        raise exception 'grocery_item_source_not_found' using errcode = 'P0002';
    end if;
end;
$$;

-- DeleteGroceryItemSources: drop the links, then lower the amounts of the items that still stay on the list.
-- item_amounts: [{"id": 1, "amount": "100 g"}, ...] (the handler worked the new amounts out).
create function public.drop_grocery_item_sources(source_ids bigint[], item_amounts jsonb)
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
    set amount = a.amount, updated_at = now()
    from jsonb_to_recordset(item_amounts) as a(id bigint, amount text)
    where g.id = a.id;
    get diagnostics changed = row_count;
    if changed <> jsonb_array_length(item_amounts) then
        raise exception 'grocery_item_not_found' using errcode = 'P0002';
    end if;
end;
$$;

-- AddPlanIngredientsToGroceryList: update the amounts of existing items, insert the new items, and link every entry to
-- its item.
-- existing_items: [{"id": 1, "amount": "300 g"}, ...]
-- new_items:      [{"key": 0, "name": "Flour", "amount": "200 g"}, ...]   (key: the handler's number for the new item)
-- sources:        [{"item_id": 1, "new_item_key": null, "planned_meal_id": 5, "ingredient_id": 9, "amount": "100 g"}, ...]
--                 (item_id for an existing item, new_item_key for one of new_items)
-- Returns {"updated": [ids of existing_items], "inserted": [ids of new_items, in key order]}.
create function public.add_plan_ingredients_to_grocery_list(existing_items jsonb, new_items jsonb, sources jsonb)
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
    set amount = e.amount, updated_at = now()
    from jsonb_to_recordset(existing_items) as e(id bigint, amount text)
    where g.id = e.id;
    get diagnostics changed = row_count;
    if changed <> jsonb_array_length(existing_items) then
        raise exception 'grocery_item_not_found' using errcode = 'P0002';
    end if;

    for new_item in
        select n.key, n.name, n.amount
        from jsonb_to_recordset(new_items) as n(key integer, name text, amount text)
        order by n.key
    loop
        insert into public.grocery_items (name, amount, is_picked, is_manual, created_by, created_at)
        values (new_item.name, new_item.amount, false, false, (select auth.uid()), now())
        returning id into new_id;

        id_of_key := id_of_key || jsonb_build_object(new_item.key::text, new_id);
        inserted_ids := inserted_ids || new_id;
    end loop;

    insert into public.grocery_item_sources (fk_grocery_item, fk_planned_meal, fk_ingredient, amount, created_by, created_at)
    select coalesce(s.item_id, (id_of_key ->> s.new_item_key::text)::bigint),
           s.planned_meal_id, s.ingredient_id, s.amount, (select auth.uid()), now()
    from jsonb_to_recordset(sources)
        as s(item_id bigint, new_item_key integer, planned_meal_id bigint, ingredient_id bigint, amount text);
    get diagnostics linked = row_count;
    if linked <> jsonb_array_length(sources) then
        raise exception 'grocery_item_source_not_added' using errcode = 'P0002';
    end if;

    return jsonb_build_object(
        'updated', coalesce((select jsonb_agg(e.id) from jsonb_to_recordset(existing_items) as e(id bigint)), '[]'::jsonb),
        'inserted', to_jsonb(inserted_ids));
end;
$$;

-- SaveGeneratedWeek: find or create the week's plan, then put a meal on each day (a day that already has one is
-- overwritten in place, so its row keeps its id).
-- days: [{"date": "2026-11-16", "meal_id": 3}, ...]
create function public.save_generated_week(week_start date, days jsonb)
returns void
language plpgsql
security invoker
set search_path = ''
as $$
declare
    plan_id bigint;
begin
    select p.id into plan_id
    from public.meal_plans p
    where p.start_date <= week_start and p.end_date >= week_start
    order by p.id
    limit 1;

    if plan_id is null then
        insert into public.meal_plans (start_date, end_date, created_by, created_at)
        values (week_start, week_start + 6, (select auth.uid()), now())
        returning id into plan_id;
    end if;

    insert into public."meal_plans-meals" (meal_date, fk_meal, fk_meal_plan, created_by, created_at)
    select d.date, d.meal_id, plan_id, (select auth.uid()), now()
    from jsonb_to_recordset(days) as d(date date, meal_id bigint)
    on conflict (fk_meal_plan, meal_date)
    do update set fk_meal = excluded.fk_meal, updated_at = now();
end;
$$;

-- Signed-in users only.
revoke all on function public.unlink_grocery_items(bigint[]) from public, anon;
revoke all on function public.update_linked_grocery_amounts(jsonb, jsonb) from public, anon;
revoke all on function public.drop_grocery_item_sources(bigint[], jsonb) from public, anon;
revoke all on function public.add_plan_ingredients_to_grocery_list(jsonb, jsonb, jsonb) from public, anon;
revoke all on function public.save_generated_week(date, jsonb) from public, anon;

grant execute on function public.unlink_grocery_items(bigint[]) to authenticated, service_role;
grant execute on function public.update_linked_grocery_amounts(jsonb, jsonb) to authenticated, service_role;
grant execute on function public.drop_grocery_item_sources(bigint[], jsonb) to authenticated, service_role;
grant execute on function public.add_plan_ingredients_to_grocery_list(jsonb, jsonb, jsonb) to authenticated, service_role;
grant execute on function public.save_generated_week(date, jsonb) to authenticated, service_role;
