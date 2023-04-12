using RAWSimO.Core.Items;
using RAWSimO.Core.Management;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RAWSimO.Core.Elements
{
    public class Compartment
    {
        public Compartment(string name, double capacity)
        {
            Name = name;
            Capacity = capacity;
        }

        /// <summary>
        /// The set of bundles not yet allocated but already registered with this pod.
        /// </summary>
        private HashSet<ItemBundle> _registeredBundles = new HashSet<ItemBundle>();
        /// <summary>
        /// The set of bundles registered for this pod.
        /// </summary>
        public IEnumerable<ItemBundle> RegisteredBundles { get { return _registeredBundles; } }

        internal string Name;

        /// <summary>
        /// The capacity of this pod.
        /// </summary>
        internal double Capacity;

        /// <summary>
        /// The amount of capacity currently in use.
        /// </summary>
        internal double CapacityInUse;

        /// <summary>
        /// The amount of capacity that is currently reserved by a controller.
        /// </summary>
        internal double CapacityReserved;

        /// <summary>
        /// Reserves capacity of this pod for the given bundle. The reserved capacity will be maintained when the bundle is allocated.
        /// </summary>
        /// <param name="bundle">The bundle for which capacity shall be reserved.</param>
        internal void RegisterBundle(ItemBundle bundle)
        {
            _registeredBundles.Add(bundle);
            CapacityReserved = _registeredBundles.Sum(b => b.BundleWeight);
            if (CapacityInUse + CapacityReserved > Capacity)
                throw new InvalidOperationException("Cannot reserve more capacity than this pod has!");
        }

        public void Add(ItemBundle itemBundle, InsertRequest insertRequest = null)
        {
                // Keep track of weight
                CapacityInUse += itemBundle.BundleWeight;
                // Keep track of reserved space
                _registeredBundles.Remove(itemBundle);
                CapacityReserved = _registeredBundles.Sum(b => b.BundleWeight);
                // Keep track of items actually contained in this pod
        }

        public bool FitsForReservation(ItemBundle bundle)
        {
            return CapacityInUse + CapacityReserved + bundle.BundleWeight <= Capacity;
        }
    }
}
