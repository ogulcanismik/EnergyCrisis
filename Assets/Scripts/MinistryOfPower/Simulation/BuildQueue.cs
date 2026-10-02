using System.Collections.Generic;

namespace MinistryOfPower.Simulation
{
    public sealed class BuildQueue
    {
        private readonly List<BuildOrder> _orders = new List<BuildOrder>(8);
        private int _nextId = 1;

        public IReadOnlyList<BuildOrder> Orders => _orders;

        public string NextOrderId()
        {
            return $"build_{_nextId++}";
        }

        public void Enqueue(BuildOrder order)
        {
            _orders.Add(order);
        }

        public bool TryCancel(string orderId)
        {
            for (int i = 0; i < _orders.Count; i++)
            {
                if (_orders[i].Id == orderId && !_orders[i].IsComplete && !_orders[i].IsCancelled)
                {
                    _orders[i].Cancel();
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Advances constructions one quarter. Returns total quarterly charges and completed plants.
        /// </summary>
        public float TickQuarter(List<PlantInstance> completedScratch)
        {
            completedScratch.Clear();
            float charges = 0f;

            for (int i = _orders.Count - 1; i >= 0; i--)
            {
                BuildOrder order = _orders[i];
                if (order.IsCancelled)
                {
                    _orders.RemoveAt(i);
                    continue;
                }

                charges += order.TickQuarter();
                if (order.IsComplete && !order.IsCancelled)
                {
                    completedScratch.Add(order.ToPlant($"online_{order.Id}"));
                    _orders.RemoveAt(i);
                }
            }

            return charges;
        }
    }
}
