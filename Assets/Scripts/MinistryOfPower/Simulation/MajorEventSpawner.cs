namespace MinistryOfPower.Simulation
{
    /// <summary>
    /// State-dependent major events, weighted by season / month / year phase.
    /// Authored flavour variants per event type (not only weight multipliers).
    /// </summary>
    public static class MajorEventSpawner
    {
        public static bool TrySpawn(
            PlantPortfolio portfolio,
            DayModifiers modifiers,
            GameClock clock,
            DeterministicRng rng,
            out PendingEvent evt,
            System.Collections.Generic.IReadOnlyList<EventWeightConfig> deckWeights = null)
        {
            evt = null;

            float oilShare = portfolio.OilLinkedShare();
            float hydroShare = portfolio.FuelShare(FuelKind.Hydro);
            float importDependence = modifiers.ImportMwBaseline <= 1f
                ? 0f
                : modifiers.ImportMwBaseline / (portfolio.TotalCapacityMw() + modifiers.ImportMwBaseline);
            float storageShare = portfolio.FuelShare(FuelKind.Storage);
            float peakerCover = portfolio.FossilShare() + storageShare;
            float weatherShare = portfolio.FuelShare(FuelKind.Wind) + portfolio.FuelShare(FuelKind.Solar);

            float wOil = 0.32f + oilShare * 0.48f;
            float wDrought = 0.14f + hydroShare * 0.85f;
            float wHeat = 0.22f + (1f - peakerCover) * 0.32f;
            float wImport = 0.11f + importDependence * 0.8f;
            float wCold = 0.1f + (1f - peakerCover) * 0.25f;
            float wStorm = 0.1f + weatherShare * 0.55f;

            ApplyDeckWeights(deckWeights, ref wOil, ref wDrought, ref wHeat, ref wImport, ref wCold, ref wStorm);
            ApplyPeriodWeights(clock, ref wOil, ref wDrought, ref wHeat, ref wImport, ref wCold, ref wStorm);

            float total = wOil + wDrought + wHeat + wImport + wCold + wStorm;
            if (total <= 0.001f)
            {
                return false;
            }

            float roll = rng.NextFloat01() * total;
            Season season = clock.CurrentSeason;
            float cursor = 0f;
            cursor += wOil;
            if (roll < cursor) evt = BuildOil(oilShare, season, rng);
            else
            {
                cursor += wDrought;
                if (roll < cursor) evt = BuildDrought(hydroShare, season, rng);
                else
                {
                    cursor += wHeat;
                    if (roll < cursor) evt = BuildHeatwave(peakerCover, storageShare, season, rng);
                    else
                    {
                        cursor += wImport;
                        if (roll < cursor) evt = BuildImport(importDependence, season, rng);
                        else
                        {
                            cursor += wCold;
                            if (roll < cursor) evt = BuildColdSnap(peakerCover, storageShare, season, rng);
                            else evt = BuildStorm(weatherShare, importDependence, season, rng);
                        }
                    }
                }
            }

            if (evt != null)
            {
                evt.Body += $"\n\n({season}, {clock.FormatDate()})";
            }

            return evt != null;
        }

        public static void ApplyDeckWeights(
            System.Collections.Generic.IReadOnlyList<EventWeightConfig> deck,
            ref float wOil,
            ref float wDrought,
            ref float wHeat,
            ref float wImport,
            ref float wCold,
            ref float wStorm)
        {
            if (deck == null || deck.Count == 0)
            {
                return;
            }

            float oil = 0f, drought = 0f, heat = 0f, import = 0f, cold = 0f, storm = 0f;
            for (int i = 0; i < deck.Count; i++)
            {
                EventWeightConfig e = deck[i];
                if (e == null) continue;
                switch (e.Kind)
                {
                    case PendingEventKind.OilPriceShock: oil += e.BaseWeight; break;
                    case PendingEventKind.Drought: drought += e.BaseWeight; break;
                    case PendingEventKind.Heatwave: heat += e.BaseWeight; break;
                    case PendingEventKind.ImportDisruption: import += e.BaseWeight; break;
                    case PendingEventKind.ColdSnap: cold += e.BaseWeight; break;
                    case PendingEventKind.StormOutage: storm += e.BaseWeight; break;
                }
            }

            if (oil > 0f) wOil *= oil;
            if (drought > 0f) wDrought *= drought;
            if (heat > 0f) wHeat *= heat;
            if (import > 0f) wImport *= import;
            if (cold > 0f) wCold *= cold;
            if (storm > 0f) wStorm *= storm;
        }

        public static void ApplyPeriodWeights(
            GameClock clock,
            ref float wOil,
            ref float wDrought,
            ref float wHeat,
            ref float wImport,
            ref float wCold,
            ref float wStorm)
        {
            Season season = clock.CurrentSeason;
            int month = clock.Month;
            int campaignYear = clock.Year - 2026;

            switch (season)
            {
                case Season.Summer:
                    wHeat *= 2.2f;
                    wDrought *= 1.6f;
                    wStorm *= 1.35f;
                    wOil *= 0.85f;
                    wCold *= 0.15f;
                    break;
                case Season.Winter:
                    wOil *= 1.35f;
                    wImport *= 1.4f;
                    wCold *= 2.4f;
                    wHeat *= 0.2f;
                    wDrought *= 0.45f;
                    wStorm *= 1.25f;
                    break;
                case Season.Spring:
                    wDrought *= 1.15f;
                    wImport *= 1.1f;
                    wStorm *= 1.2f;
                    wCold *= 0.7f;
                    break;
                case Season.Autumn:
                    wOil *= 1.15f;
                    wHeat *= 0.7f;
                    wStorm *= 1.4f;
                    wCold *= 0.85f;
                    break;
            }

            if (month >= 7 && month <= 9) wDrought *= 1.35f;
            if (month == 12 || month <= 2)
            {
                wImport *= 1.25f;
                wOil *= 1.2f;
                wCold *= 1.3f;
            }

            if (campaignYear <= 2) wOil *= 1.25f;
            else if (campaignYear >= 8)
            {
                wHeat *= 1.15f;
                wDrought *= 1.1f;
                wStorm *= 1.1f;
            }
        }

        public static PendingEvent BuildMeterCrash(SeatMeters meters, Season season = Season.Spring)
        {
            bool adequacyFirst = meters.Adequacy <= meters.Affordability;
            string title;
            string body;
            if (adequacyFirst)
            {
                title = season == Season.Winter ? "Winter Adequacy Crisis" : "Adequacy Crisis";
                body = season == Season.Winter
                    ? Flavour(season, meters,
                        "Thermometers and the scoreboard agree: the grid is short. Pick who goes dark — industry, suburbs, or transit — or burn emergency fossil.",
                        "Cold snap + thin firm cover. Cabinet wants a load-shed order before the evening news.",
                        "Heating season, red adequacy. Someone loses power tonight unless you write a fossil cheque.")
                    : Flavour(season, meters,
                        "No shock card — the daily scoreboard broke. Adequacy is red. Choose a load-shed target or buy time with emergency fossil.",
                        "Lights are the story. Ministerial choice: who eats the dark, or how much treasury you torch.",
                        "Adequacy crashed without a weather excuse. Own the shed list or own the fossil bill.");
            }
            else
            {
                title = "Affordability Crisis";
                body = Flavour(season, meters,
                    "Bill pain crossed the line. Absorb the headlines or freeze tariffs / burn emergency fuel.",
                    "Households are loud. Absorb, freeze tariffs, or paper over with emergency fossil.",
                    "Tariff riots in the brief. Absorb, freeze, or bribe the night with fossil MW.");
            }

            return new PendingEvent
            {
                Kind = PendingEventKind.MeterCrash,
                Title = title,
                Body = body,
                ExposureLabel = adequacyFirst ? "Adequacy" : "Affordability",
                Exposure01 = (adequacyFirst ? meters.Adequacy : meters.Affordability) / 100f,
                Severity01 = Clamp01(1f - (adequacyFirst ? meters.Adequacy : meters.Affordability) / 55f),
                AwaitingDecision = true,
                OffersLoadShed = adequacyFirst
            };
        }

        private static string Flavour(Season season, SeatMeters meters, string a, string b, string c)
        {
            int h = (int)(meters.Adequacy + meters.Affordability * 3f) + (int)season;
            int pick = h % 3;
            if (pick == 0) return a;
            if (pick == 1) return b;
            return c;
        }

        private static string Pick(DeterministicRng rng, params string[] options)
        {
            if (options == null || options.Length == 0) return "";
            int i = (int)(rng.NextFloat01() * options.Length);
            if (i >= options.Length) i = options.Length - 1;
            return options[i];
        }

        private static PendingEvent BuildOil(float oilShare, Season season, DeterministicRng rng)
        {
            float severity = Clamp01(oilShare * 0.85f + rng.NextRange(0f, 0.25f));
            string title;
            string body;

            if (oilShare < 0.15f)
            {
                title = Pick(rng, "Overseas Crude Spike", "Oil Price Shock", "Bench Marker Panic");
                body = season == Season.Winter
                    ? Pick(rng,
                        "Crude spiked. Your books are mostly off oil/gas — winter heating still makes the papers, not the meters.",
                        "Futures scream; your mix barely listens. File under noise.")
                    : Pick(rng,
                        "Crude spiked overseas. Grid mostly off oil/gas — newspapers shrug; you file it.",
                        "Tanker drama abroad. Exposure thin — desk stays quiet.");
                severity *= 0.25f;
            }
            else if (oilShare < 0.4f)
            {
                title = season == Season.Winter
                    ? Pick(rng, "Winter Fuel Spike", "Heating Oil Shock", "Cold-Market Oil Bite")
                    : Pick(rng, "Mid-Transition Oil Spike", "Oil Price Shock", "Partial Exposure Spike");
                body = season == Season.Winter
                    ? Pick(rng,
                        "Cold + oil spike. Partial exposure: gas peakers get expensive exactly when you need them.",
                        "Winter fuel markets bite. Mid-transition grid: bills twitch, adequacy softens.",
                        "Heating oil headlines. You're half-insured — half-hostage.")
                    : Pick(rng,
                        "Oil spike hits mid-transition. Partial exposure — bills twitch, adequacy soft.",
                        "Tankers and futures scream. You're half-clean, half-hostage.",
                        "Brent jumps. Gas peakers remind you the transition is unfinished.");
            }
            else
            {
                title = season == Season.Summer
                    ? Pick(rng, "Summer Fuel Squeeze", "Oil Price Shock", "AC-Season Crude Hit")
                    : Pick(rng, "Oil Spike — Heavy Exposure", "Oil Price Shock", "Fossil Ledger Crisis");
                body = season == Season.Summer
                    ? Pick(rng,
                        "Oil skyrockets into AC season. Heavy gas/oil exposure — peakers will price you into a crisis.",
                        "Fuel shock under summer load. Budget and bills take the punch unless you intervene.",
                        "Crude under heatwave. Your oil-linked MW is a liability with a bill.")
                    : Pick(rng,
                        "Oil prices skyrocket. Heavy gas/oil exposure — budget and bills take the punch unless you intervene.",
                        "Your mix is still chained to oil/gas. This one will leave bruises.",
                        "Heavy oil/gas books meet a price spike. Absorb or postpone — both hurt.");
            }

            return new PendingEvent
            {
                Kind = PendingEventKind.OilPriceShock,
                Title = title,
                Body = body,
                ExposureLabel = "Oil/gas-linked share",
                Exposure01 = oilShare,
                Severity01 = severity,
                AwaitingDecision = oilShare >= 0.15f || severity > 0.35f
            };
        }

        private static PendingEvent BuildDrought(float hydroShare, Season season, DeterministicRng rng)
        {
            float severity = Clamp01(hydroShare * 1.1f + rng.NextRange(0f, 0.2f));
            string title;
            string body;

            if (hydroShare < 0.08f)
            {
                title = Pick(rng, "Dry Weather Bulletin", "Hydro Drought", "Rain Shadow Note");
                body = Pick(rng,
                    "Regional drought. Little hydro on your books — a weather story, not a ministry crisis.",
                    "Dry soils inland. Hydro exposure thin — shrug and archive.");
                severity *= 0.2f;
            }
            else if (hydroShare < 0.22f)
            {
                title = season == Season.Summer || season == Season.Autumn
                    ? Pick(rng, "Reservoir Warning", "Hydro Drought", "Low Pool Alert")
                    : Pick(rng, "Low Inflow Season", "Hydro Drought", "Catchment Shortfall");
                body = season == Season.Summer
                    ? Pick(rng,
                        "Dry reservoirs under summer demand. Moderate hydro dependence: adequacy softens if you do nothing.",
                        "Summer drought. Hydro dips while AC climbs — classic ministry headache.",
                        "Pools fall as AC rises. Hydro is a supporting pillar — evenings get soft.")
                    : Pick(rng,
                        "Dry reservoirs. Moderate hydro dependence: adequacy softens if you do nothing.",
                        "Inflows weak. Hydro is a supporting pillar — expect soft evenings.",
                        "Catchments thin. Hydro support wobbles; schedule the hole.");
            }
            else
            {
                title = Pick(rng, "Severe Hydro Drought", "Hydro Drought", "Dam Crisis");
                body = season == Season.Summer
                    ? Pick(rng,
                        "Severe drought. Hydro is a pillar — multi-week hole under peak AC load.",
                        "Reservoirs empty into a hot season. Hydro pillar cracked.",
                        "The dams aren't coming back soon under summer load. Plan the hole, or pay for fossil.")
                    : Pick(rng,
                        "Severe drought. Hydro is a pillar of your mix — expect a multi-week output hole.",
                        "The dams aren't coming back soon. Plan the hole, or pay for fossil.",
                        "Major catchment failure. Hydro pillar offline — fossil or shed.");
            }

            return new PendingEvent
            {
                Kind = PendingEventKind.Drought,
                Title = title,
                Body = body,
                ExposureLabel = "Hydro share",
                Exposure01 = hydroShare,
                Severity01 = severity,
                AwaitingDecision = hydroShare >= 0.08f || severity > 0.3f
            };
        }

        private static PendingEvent BuildHeatwave(float peakerCover, float storageShare, Season season, DeterministicRng rng)
        {
            float vulnerability = Clamp01(1f - peakerCover * 0.85f - storageShare * 0.5f);
            float severity = Clamp01(vulnerability * 0.9f + rng.NextRange(0f, 0.2f));
            string title;
            string body;

            if (vulnerability < 0.25f)
            {
                title = Pick(rng, "Heat Advisory", "Heatwave Demand Spike", "Warm Spell Notice");
                body = Pick(rng,
                    "Heatwave warning. Peakers and storage look adequate — demand bump should be containable.",
                    "Hot air mass inbound. Firm cover looks thick enough to shrug.");
                severity *= 0.35f;
            }
            else if (vulnerability < 0.55f)
            {
                title = season == Season.Summer
                    ? Pick(rng, "Prolonged Heat Event", "Heatwave Demand Spike", "Multi-Day Furnace")
                    : Pick(rng, "Unseasonal Heat Spike", "Heatwave Demand Spike", "Shoulder-Season Heat");
                body = Pick(rng,
                    "Prolonged heat. Evening peaks will test thin reserve margins.",
                    "AC load climbs into the evening. Your peaker/storage cover is merely 'fine' — which means not fine.",
                    "Heat builds. Margins look polite on paper and thin after dinner.");
            }
            else
            {
                title = Pick(rng, "Extreme Heatwave", "Heatwave Demand Spike", "Grid Furnace Alert");
                body = Pick(rng,
                    "Extreme heatwave. Weak peaker/storage cover — adequacy risk spikes with AC load.",
                    "The forecast is a furnace. Thin firm cover: this is an adequacy story whether you like it or not.",
                    "Scorching demand, thin firm MW. Absorb, fossil, or freeze — pick your bruise.");
            }

            return new PendingEvent
            {
                Kind = PendingEventKind.Heatwave,
                Title = title,
                Body = body,
                ExposureLabel = "Peak vulnerability",
                Exposure01 = vulnerability,
                Severity01 = severity,
                AwaitingDecision = vulnerability >= 0.25f || severity > 0.35f
            };
        }

        private static PendingEvent BuildImport(float importDependence, Season season, DeterministicRng rng)
        {
            float severity = Clamp01(importDependence * 1.2f + rng.NextRange(0f, 0.2f));
            string title;
            string body;

            if (importDependence < 0.08f)
            {
                title = Pick(rng, "Neighbor Cable Fault", "Import Disruption", "Border Line Blip");
                body = Pick(rng,
                    "Neighboring cable fault. You barely rely on imports — shrug.",
                    "Interconnector hiccup. Domestic books carry the night.");
                severity *= 0.2f;
            }
            else if (importDependence < 0.2f)
            {
                title = season == Season.Winter
                    ? Pick(rng, "Winter Interconnector Cut", "Import Disruption", "Cold Cable Constraint")
                    : Pick(rng, "Cable Constraint", "Import Disruption", "Neighbor Soft Cap");
                body = season == Season.Winter
                    ? Pick(rng,
                        "Import disruption in winter. Moderate cable dependence: a few tight, cold evenings ahead.",
                        "Interconnector soft under heating load. Domestic margin is the story.",
                        "Neighbors blinked into a freeze. You're not addicted — evenings get interesting.")
                    : Pick(rng,
                        "Import disruption. Moderate cable dependence: a few tight evenings ahead.",
                        "Neighbors blinked. You're not addicted — yet — but evenings get interesting.",
                        "Cable constraint notice. Domestic peaking fills the polite fiction.");
            }
            else
            {
                title = Pick(rng, "Major Import Cut", "Import Disruption", "Interconnector Crisis");
                body = season == Season.Winter
                    ? Pick(rng,
                        "Major import cut into winter. Interconnector shortfall exposes how hard you lean on neighbors.",
                        "The cable's gone quiet and the cold isn't. Import dependence is now a ministry crisis.",
                        "Cross-border MW vanished into a freeze. Domestic margin was a polite fiction.")
                    : Pick(rng,
                        "Major import cut. Interconnector shortfall exposes how much you lean on neighbors.",
                        "Cross-border MW vanished. Your domestic margin was a polite fiction.",
                        "Interconnector crisis. Neighbor MW gone — schedule the bruise.");
            }

            return new PendingEvent
            {
                Kind = PendingEventKind.ImportDisruption,
                Title = title,
                Body = body,
                ExposureLabel = "Import dependence",
                Exposure01 = importDependence,
                Severity01 = severity,
                AwaitingDecision = importDependence >= 0.08f || severity > 0.3f
            };
        }

        private static PendingEvent BuildColdSnap(float peakerCover, float storageShare, Season season, DeterministicRng rng)
        {
            float vulnerability = Clamp01(1f - peakerCover * 0.9f - storageShare * 0.4f);
            float severity = Clamp01(vulnerability * 0.85f + rng.NextRange(0f, 0.22f));
            if (season != Season.Winter && season != Season.Autumn) severity *= 0.55f;

            string title = season == Season.Winter
                ? Pick(rng, "Arctic Cold Snap", "Deep Freeze Demand", "Polar Plunge Peak")
                : Pick(rng, "Sudden Cold Snap", "Unseasonal Freeze", "Heating Spike Alert");
            string body;
            if (vulnerability < 0.28f)
            {
                body = Pick(rng,
                    "Hard freeze inbound. Firm cover looks thick — heating bump should be containable.",
                    "Cold air mass. Peakers and storage look ready.");
                severity *= 0.35f;
            }
            else
            {
                body = Pick(rng,
                    "Deep freeze. Heating load climbs overnight — thin firm cover means adequacy risk.",
                    "Polar air + thin peaker/storage. Evening and dawn peaks will bruise the desk.",
                    "Cold snap. Gas and storage margins decide whether this is news or a crisis.");
            }

            return new PendingEvent
            {
                Kind = PendingEventKind.ColdSnap,
                Title = title,
                Body = body,
                ExposureLabel = "Winter peak vulnerability",
                Exposure01 = vulnerability,
                Severity01 = severity,
                AwaitingDecision = vulnerability >= 0.28f || severity > 0.35f
            };
        }

        private static PendingEvent BuildStorm(float weatherShare, float importDependence, Season season, DeterministicRng rng)
        {
            float exposure = Clamp01(weatherShare * 0.7f + importDependence * 0.35f);
            float severity = Clamp01(exposure * 0.9f + rng.NextRange(0f, 0.2f));
            string title = Pick(rng, "Grid Storm Outage", "Cyclone Line Damage", "Renewable Storm Cut");
            string body;
            if (exposure < 0.12f)
            {
                body = Pick(rng,
                    "Storm cells inland. Little weather-linked MW on your books — line crews, not ministers.",
                    "Wind advisory. Exposure thin — file under weather desk.");
                severity *= 0.25f;
            }
            else
            {
                body = Pick(rng,
                    "Storm takes wind/solar offline and softens interconnectors. Adequacy softens with the weather.",
                    "Cyclone damage: renewables derate, cables blink. Absorb or buy firm cover.",
                    "Weather outage. Variable MW and neighbors both wobble — classic dual exposure.");
            }

            return new PendingEvent
            {
                Kind = PendingEventKind.StormOutage,
                Title = title,
                Body = body,
                ExposureLabel = "Weather + cable exposure",
                Exposure01 = exposure,
                Severity01 = severity,
                AwaitingDecision = exposure >= 0.12f || severity > 0.32f
            };
        }

        private static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }
    }
}
