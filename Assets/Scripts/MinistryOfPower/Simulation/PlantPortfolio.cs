using System.Collections.Generic;

namespace MinistryOfPower.Simulation
{
    public sealed class PlantPortfolio
    {
        private readonly List<PlantInstance> _plants = new List<PlantInstance>(32);
        private int _nextId = 1;

        public IReadOnlyList<PlantInstance> Plants => _plants;

        public void Add(PlantInstance plant)
        {
            _plants.Add(plant);
        }

        public string NextPlantId(string prefix)
        {
            return $"{prefix}_{_nextId++}";
        }

        public bool TryRetire(string plantId, out PlantInstance retired)
        {
            for (int i = 0; i < _plants.Count; i++)
            {
                PlantInstance plant = _plants[i];
                if (plant.Id == plantId && !plant.IsRetired)
                {
                    plant.Retire();
                    retired = plant;
                    return true;
                }
            }

            retired = null;
            return false;
        }

        public float TotalCapacityMw(bool activeOnly = true)
        {
            float total = 0f;
            for (int i = 0; i < _plants.Count; i++)
            {
                PlantInstance p = _plants[i];
                if (activeOnly && p.IsRetired)
                {
                    continue;
                }

                total += p.CapacityMw;
            }

            return total;
        }

        public float CleanCapacityMw()
        {
            float total = 0f;
            for (int i = 0; i < _plants.Count; i++)
            {
                PlantInstance p = _plants[i];
                if (p.IsRetired || !p.Fuel.IsClean())
                {
                    continue;
                }

                total += p.CapacityMw;
            }

            return total;
        }

        public float FossilCapacityMw()
        {
            float total = 0f;
            for (int i = 0; i < _plants.Count; i++)
            {
                PlantInstance p = _plants[i];
                if (p.IsRetired || !p.Fuel.IsFossil())
                {
                    continue;
                }

                total += p.CapacityMw;
            }

            return total;
        }

        public float OilLinkedShare()
        {
            float total = 0f;
            float oilLinked = 0f;
            for (int i = 0; i < _plants.Count; i++)
            {
                PlantInstance p = _plants[i];
                if (p.IsRetired)
                {
                    continue;
                }

                total += p.CapacityMw;
                if (p.Fuel.IsOilLinked())
                {
                    oilLinked += p.CapacityMw * (0.5f + 0.5f * p.OilExposure);
                }
            }

            if (total <= 0.001f)
            {
                return 0f;
            }

            return oilLinked / total;
        }

        public float FossilShare()
        {
            float total = TotalCapacityMw();
            if (total <= 0.001f)
            {
                return 0f;
            }

            return FossilCapacityMw() / total;
        }

        public float FuelShare(FuelKind fuel)
        {
            float total = TotalCapacityMw();
            if (total <= 0.001f)
            {
                return 0f;
            }

            float amount = 0f;
            for (int i = 0; i < _plants.Count; i++)
            {
                PlantInstance p = _plants[i];
                if (!p.IsRetired && p.Fuel == fuel)
                {
                    amount += p.CapacityMw;
                }
            }

            return amount / total;
        }

        public float TransitionProgress01()
        {
            float total = TotalCapacityMw();
            if (total <= 0.001f)
            {
                return 0f;
            }

            return CleanCapacityMw() / total;
        }

        public float SumEffectiveSupply(float solarFactor, float windFactor, float hydroFactor)
        {
            float supply = 0f;
            for (int i = 0; i < _plants.Count; i++)
            {
                PlantInstance p = _plants[i];
                float weather = 1f;
                switch (p.Fuel)
                {
                    case FuelKind.Solar: weather = solarFactor; break;
                    case FuelKind.Wind: weather = windFactor; break;
                    case FuelKind.Hydro: weather = hydroFactor; break;
                }

                supply += p.EffectiveCapacityMw(weather);
            }

            return supply;
        }

        public float EstimateMarginalCost(float fuelPriceIndex, float oilShockMultiplier)
        {
            float bestCost = float.MaxValue;
            bool found = false;
            for (int i = 0; i < _plants.Count; i++)
            {
                PlantInstance p = _plants[i];
                if (p.IsRetired || p.CapacityMw <= 0f)
                {
                    continue;
                }

                float cost = p.VariableCostPerMwh * fuelPriceIndex;
                if (p.Fuel.IsOilLinked())
                {
                    cost *= 1f + p.OilExposure * (oilShockMultiplier - 1f);
                }

                if (cost < bestCost)
                {
                    bestCost = cost;
                    found = true;
                }
            }

            return found ? bestCost : 80f;
        }
    }
}
