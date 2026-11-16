namespace MealWiz.Shared.Features.GroceryCategories.Models;

/// <summary>
/// The built-in English and Danish grocery words, each mapped to a default category. Both languages are always active,
/// whatever the app language. Entries are lower case; a few are whole names of more than one word ("olive oil"),
/// which <see cref="CategoryResolver"/> matches before single words. Other has no words: it is where unknown names go.
/// </summary>
public static class CategoryDictionary
{
    private static readonly (DefaultCategoryKey Key, string English, string Danish)[] Sections =
    [
        (DefaultCategoryKey.Produce,
            """
            apple apples banana bananas orange oranges lemon lemons lime limes pear pears grape grapes strawberry
            strawberries raspberry raspberries blueberry blueberries berries melon watermelon pineapple mango mangoes
            kiwi avocado avocados peach peaches plum plums cherry cherries nectarine nectarines clementine clementines
            tomato tomatoes potato potatoes onion onions shallot shallots garlic carrot carrots cucumber cucumbers
            lettuce salad spinach kale cabbage broccoli cauliflower celery celeriac leek leeks zucchini courgette
            courgettes aubergine eggplant mushroom mushrooms asparagus beetroot beetroots radish radishes parsnip
            parsnips ginger chili chilli chillies chilies parsley basil coriander cilantro dill mint chives rosemary
            thyme scallion scallions rocket arugula fennel pumpkin squash butternut herbs fruit vegetables veggies
            sprouts|bell pepper|bell peppers|red pepper|sweet potato|sweet potatoes|spring onion|spring onions|green beans
            """,
            """
            æble æbler banan bananer appelsin appelsiner citron citroner lime pære pærer vindruer druer jordbær
            hindbær blåbær brombær bær melon vandmelon ananas mango kiwi avocado avocadoer fersken ferskner blomme
            blommer kirsebær nektarin nektariner klementin klementiner mandarin mandariner tomat tomater kartoffel
            kartofler løg rødløg skalotteløg hvidløg gulerod gulerødder agurk agurker salat icebergsalat spinat
            grønkål kål hvidkål rødkål spidskål rosenkål broccoli blomkål selleri bladselleri knoldselleri porre
            porrer peberfrugt peberfrugter squash aubergine champignon champignoner svampe asparges rødbede rødbeder
            radise radiser pastinak pastinakker ingefær chili persille basilikum koriander dild mynte purløg rosmarin
            timian forårsløg rucola fennikel græskar krydderurter frugt grøntsager grønt spirer|søde kartofler
            """),
        (DefaultCategoryKey.Bakery,
            """
            bread breads loaf baguette baguettes roll rolls bun buns bagel bagels croissant croissants tortilla
            tortillas wrap wraps pita pitta naan sourdough crispbread cake cakes muffin muffins pastry pastries
            brioche ciabatta focaccia scone scones flatbread|rye bread|white bread|hot dog buns|burger buns|danish pastry
            """,
            """
            brød rugbrød franskbrød toastbrød hvedebrød grovbrød boller bolle rundstykker rundstykke baguetter
            croissanter pitabrød surdejsbrød knækbrød kage kager wienerbrød kringle flute flutes pølsebrød
            burgerboller sandwichbrød fladbrød tebirkes kanelsnegle kanelsnegl
            """),
        (DefaultCategoryKey.MeatFish,
            """
            chicken beef mince steak steaks pork bacon sausage sausages lamb turkey duck veal meatballs ribs fillet
            fillets salmon cod shrimp shrimps prawns mussels fish trout haddock plaice crab lobster squid scallops
            burger burgers patties tenderloin brisket drumsticks wings meat|chicken breast|chicken breasts
            |chicken thighs|minced beef|ground beef|pork chops|pork belly|minced pork
            """,
            """
            kylling kyllingebryst kyllingelår kyllingefilet kyllingefileter kyllingevinger oksekød hakkekød oksefars
            svinekød grisekød flæsk flæskesteg pølse pølser medister lammekød kalkun kalvekød frikadeller ribben
            mørbrad laks torsk rejer muslinger fisk ørred kuller rødspætte krabbe hummer blæksprutte kammuslinger
            bøf bøffer burgerbøffer culotte entrecote kotelet koteletter schnitzel fars kød kalkunbryst andebryst
            andelår svinemørbrad nakkefilet|hakket oksekød|hakket svinekød|hel kylling|hel and
            """),
        (DefaultCategoryKey.Deli,
            """
            ham salami pepperoni prosciutto chorizo pastrami pâté pate hummus olives antipasti tapas coleslaw dip dips
            |cold cuts|smoked salmon|roast beef|liver pâté|sliced ham
            """,
            """
            skinke hamburgerryg spegepølse rullepølse serranoskinke parmaskinke leverpostej postej pålæg oliven
            roastbeef sylte rejesalat hønsesalat karrysalat tunsalat æggesalat kyllingesalat
            |røget laks|italiensk salat|makrel i tomat
            """),
        (DefaultCategoryKey.DairyEggs,
            """
            milk buttermilk cream butter cheese cheddar mozzarella parmesan feta brie camembert gouda ricotta
            mascarpone halloumi yogurt yoghurt yogurts yoghurts skyr kefir egg eggs margarine custard quark oatmilk
            |whole milk|skimmed milk|sour cream|whipping cream|double cream|single cream|crème fraîche|creme fraiche
            |cottage cheese|cream cheese|greek yogurt|oat milk|soy milk|almond milk
            """,
            """
            mælk sødmælk letmælk minimælk skummetmælk kærnemælk fløde piskefløde madlavningsfløde kaffefløde
            cremefraiche fraiche smør ost skiveost hytteost flødeost æg smørbar koldskål ymer havremælk sojamælk
            mandelmælk kvark|revet ost|creme fraiche
            """),
        (DefaultCategoryKey.Frozen,
            """
            frozen peas icecream sorbet popsicle popsicles fries edamame|ice cream|frozen pizza|fish fingers
            |frozen vegetables|frozen berries|ice cubes|french fries|ice lolly|frozen spinach
            """,
            """
            frost frosne frossen ærter is isvafler flødeis vaniljeis pizza fiskepinde fritter isterninger
            |frosne ærter|pommes frites|frosne grøntsager|frosne bær|frossen pizza
            """),
        (DefaultCategoryKey.Pantry,
            """
            pasta spaghetti penne macaroni lasagne lasagna noodles rice basmati flour sugar salt pepper oil vinegar
            ketchup mustard mayonnaise mayo sauce stock broth bouillon passata beans chickpeas lentils oats oatmeal
            porridge cereal muesli granola cornflakes honey jam nutella yeast vanilla cinnamon cumin paprika curry
            spices oregano couscous quinoa bulgur breadcrumbs tuna sweetcorn corn raisins cocoa tahini salsa pesto
            soup cornstarch|olive oil|soy sauce|stock cube|canned tomatoes|tomato paste|tomato puree|kidney beans
            |coconut milk|peanut butter|baking powder|baking soda|black pepper
            """,
            """
            spaghetti makaroni lasagneplader nudler ris basmatiris jasminris risottoris mel hvedemel rugmel sukker
            rørsukker flormelis peber olie olivenolie rapsolie eddike sennep remoulade sojasauce fond
            bouillonterning tomatpuré bønner kidneybønner kikærter linser kokosmælk havregryn morgenmad müsli mysli
            honning marmelade syltetøj peanutbutter jordnøddesmør bagepulver natron gær vanilje vaniljesukker kanel
            spidskommen karry krydderier rasp tun majs rosiner kakao tahin suppe maizena husblas dåsetomater
            chokoladepålæg
            """),
        (DefaultCategoryKey.Drinks,
            """
            water juice soda cola coke lemonade beer wine rosé cider coffee tea tonic smoothie prosecco champagne
            whisky whiskey vodka gin rum espresso kombucha|sparkling water|orange juice|apple juice|red wine
            |white wine|energy drink
            """,
            """
            vand danskvand mineralvand appelsinjuice æblejuice saft sodavand limonade øl vin rødvin hvidvin rosévin
            kaffe te energidrik rom kakaomælk
            """),
        (DefaultCategoryKey.Snacks,
            """
            chips crisps popcorn nuts peanuts cashews almonds chocolate candy sweets gummies licorice liquorice
            biscuits cookies crackers pretzels marshmallows gum snacks snack nachos|chewing gum|granola bar
            |cereal bar|tortilla chips|dark chocolate|wine gums
            """,
            """
            nødder jordnødder cashewnødder mandler chokolade slik vingummi lakrids kiks småkager småkage
            saltstænger skumfiduser tyggegummi müslibar chokoladebar flødeboller karameller bolcher pastiller
            |mørk chokolade
            """),
        (DefaultCategoryKey.Household,
            """
            tissues napkins detergent soap shampoo conditioner toothpaste toothbrush deodorant razor razors sponge
            sponges foil batteries candles cleaner bleach diapers nappies wipes sunscreen plasters lotion
            |toilet paper|kitchen roll|paper towels|dish soap|washing up liquid|laundry detergent|fabric softener
            |hand soap|bin bags|trash bags|garbage bags|cling film|baking paper|light bulbs|cotton pads
            """,
            """
            toiletpapir køkkenrulle servietter lommetørklæder opvaskemiddel opvasketabs vaskemiddel skyllemiddel
            sæbe håndsæbe balsam tandpasta tandbørste barberblade karklud karklude grydesvamp affaldsposer
            skraldeposer sølvpapir husholdningsfilm bagepapir batterier elpærer stearinlys rengøringsmiddel klorin
            bleer vådservietter vatrondeller solcreme plaster bodylotion
            """),
    ];

    // Declared after Sections: static fields initialise in order.
    /// <summary>Normalised word or name → default category.</summary>
    public static IReadOnlyDictionary<string, DefaultCategoryKey> Words { get; } = Build();

    // Single words are separated by whitespace; whole names of more than one word follow a '|'.
    private static Dictionary<string, DefaultCategoryKey> Build()
    {
        var words = new Dictionary<string, DefaultCategoryKey>(StringComparer.Ordinal);
        foreach (var (key, english, danish) in Sections)
        {
            foreach (var entry in Entries(english).Concat(Entries(danish)))
                words.TryAdd(CategoryResolver.Normalise(entry), key);
        }

        return words;
    }

    private static IEnumerable<string> Entries(string text)
    {
        var parts = text.Split('|');
        foreach (var word in parts[0].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            yield return word;
        foreach (var name in parts.Skip(1))
            if (!string.IsNullOrWhiteSpace(name)) yield return name;
    }
}
