using RAWSimO.Core.Info;
using RAWSimO.Core.Items;
using RAWSimO.Core.Management;
using RAWSimO.Toolbox;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;

namespace RAWSimO.Core.Elements
{
    public class Compartment
    {
        internal const int MAX_ITEMDESCRIPTION_COUNT_FOR_FAST_ACCESS = 1000;

        /// <summary>
        /// All items that are physically contained in this pod.
        /// </summary>
        internal HashSet<ItemDescription> _itemDescriptionsContained = new HashSet<ItemDescription>();
        /// <summary>
        /// All items that are physically contained in this pod.
        /// </summary>
        internal IEnumerable<ItemDescription> ItemDescriptionsContained { get { return _itemDescriptionsContained; } }
        /// <summary>
        /// Contains the number of items still left of the different kinds (including already reserved ones).
        /// </summary>
        private IInflexibleDictionary<ItemDescription, int> _itemDescriptionCountContained;
        /// <summary>
        /// Contains the number of items still left of the different kinds (exluding already reserved ones).
        /// </summary>
        private IInflexibleDictionary<ItemDescription, int> _itemDescriptionCountAvailable;


        public Compartment(Instance instance, string name, double capacity, Pod pod)
        {
            this.Instance = instance;
            Name = name;
            Capacity = capacity;
            Pod = pod;
        }

        internal void InitCompartmentContentInfo()
        {
            if (Instance.ItemDescriptions.Count <= MAX_ITEMDESCRIPTION_COUNT_FOR_FAST_ACCESS)
            {
                // Use fast access dictionaries
                _itemDescriptionCountContained = new VolatileIDDictionary<ItemDescription, int>(Instance.ItemDescriptions.Select(i => new VolatileKeyValuePair<ItemDescription, int>(i, 0)).ToList());
                _itemDescriptionCountAvailable = new VolatileIDDictionary<ItemDescription, int>(Instance.ItemDescriptions.Select(i => new VolatileKeyValuePair<ItemDescription, int>(i, 0)).ToList());
            }
            else
            {
                // Use ordinary dictionaries
                _itemDescriptionCountContained = new InflexibleIntDictionary<ItemDescription>(Instance.ItemDescriptions.Select(i => new KeyValuePair<ItemDescription, int>(i, 0)).ToList());
                _itemDescriptionCountAvailable = new InflexibleIntDictionary<ItemDescription>(Instance.ItemDescriptions.Select(i => new KeyValuePair<ItemDescription, int>(i, 0)).ToList());
            }
        }

        /// <summary>
        /// The set of bundles not yet allocated but already registered with this pod.
        /// </summary>
        public HashSet<ItemBundle> _registeredBundles = new HashSet<ItemBundle>();
        /// <summary>
        /// The set of bundles registered for this pod.
        /// </summary>
        public IEnumerable<ItemBundle> RegisteredBundles { get { return _registeredBundles; } }

        public Pod Pod { get; }

        private readonly Instance Instance;
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
            CapacityReserved += bundle.BundleWeight;
            //CapacityReserved = _registeredBundles.Sum(b => b.BundleWeight);
            //Debug.WriteLine($"Registered bundle {bundle.ID} in {Name}. {CapacityInUse}+{CapacityReserved}/{Capacity}.");
            if (CapacityInUse + CapacityReserved > Capacity)
                throw new InvalidOperationException("Cannot reserve more capacity than this pod has!");
        }

        public bool Add(ItemBundle itemBundle, InsertRequest insertRequest = null)
        {
            //Debug.WriteLine($"CompartmentAdd-Before. Bundle {itemBundle.ID} weight {itemBundle.BundleWeight} in {this.Name}: InUse={this.CapacityInUse}/ Reserved={this.CapacityReserved}/ Total={this.Capacity}.");
            // Init, if not done yet
            if (_itemDescriptionCountContained == null)
                InitCompartmentContentInfo();

            // Keep track of weight
            CapacityInUse += itemBundle.BundleWeight;
            // Keep track of reserved space
            _registeredBundles.Remove(itemBundle);
            //CapacityReserved = _registeredBundles.Sum(b => b.BundleWeight);
            if (CapacityReserved > 0)
            {
                CapacityReserved -= itemBundle.BundleWeight;
            }
            // Keep track of items actually contained in this pod
            if (_itemDescriptionCountContained[itemBundle.ItemDescription] <= 0)
                _itemDescriptionsContained.Add(itemBundle.ItemDescription);
            // Keep track of the number of available items on this pod (for picking)
            _itemDescriptionCountAvailable[itemBundle.ItemDescription] += itemBundle.ItemCount;
            Debug.WriteLine($"Added to {this.Name} bundle {itemBundle.ID} +{itemBundle.ItemCount} //{_itemDescriptionCountAvailable[itemBundle.ItemDescription]}");
            // Keep track of the number of contained items on this pod (for picking)
            _itemDescriptionCountContained[itemBundle.ItemDescription] += itemBundle.ItemCount;

            //Debug.WriteLine($"CompartmentAdd-After. Bundle {itemBundle.ID} weight {itemBundle.BundleWeight} in {this.Name}: InUse={this.CapacityInUse}/ Reserved={this.CapacityReserved}/ Total={this.Capacity}.");
            return true;
        }

        public void Remove(ItemDescription item, ExtractRequest extractRequest)
        {
            // Remove the item entity
            _itemDescriptionCountContained[item]--;
            // Keep track of items actually contained in this pod
            if (_itemDescriptionCountContained[item] <= 0)
                _itemDescriptionsContained.Remove(item);
            // Keep track of weight
            CapacityInUse -= item.Weight;
        }

        internal void RegisterItem(ItemDescription item, ExtractRequest extractRequest)
        {
            // Init, if not done yet
            if (_itemDescriptionCountContained == null)
                InitCompartmentContentInfo();
            if (_itemDescriptionCountAvailable[item] <= 0)
                throw new InvalidOperationException("Cannot reserve an item for picking, if there is none left of the kind!");
            _itemDescriptionCountAvailable[item]--;
            Debug.WriteLine($"Registered from {this.Name} bundle {item.ID} -1 //{_itemDescriptionCountAvailable[item]}");

        }

        internal void UnregisterItem(ItemDescription item, ExtractRequest extractRequest)
        {
            _itemDescriptionCountAvailable[item]++;
            Debug.WriteLine($"Unregistered from {this.Name} bundle {item.ID} +1 //{_itemDescriptionCountAvailable[item]}");

        }

        public bool IsContained(ItemDescription itemDescription) { return _itemDescriptionCountContained == null ? false : _itemDescriptionCountContained[itemDescription] > 0; }

        public bool IsAvailable(ItemDescription itemDescription) { return _itemDescriptionCountAvailable == null ? false : _itemDescriptionCountAvailable[itemDescription] > 0; }

        public int CountContained(ItemDescription itemDescription) { return _itemDescriptionCountContained == null ? 0 : _itemDescriptionCountContained[itemDescription]; }

        public int CountAvailable(ItemDescription itemDescription) { return _itemDescriptionCountAvailable == null ? 0 : _itemDescriptionCountAvailable[itemDescription]; }

        public int GetInfoContent(IItemDescriptionInfo item) { return _itemDescriptionCountContained != null ? _itemDescriptionCountContained[item as ItemDescription] : 0; }

        public bool FitsForReservation(ItemBundle bundle)
        {
            return (CapacityInUse + CapacityReserved == 0) && (CapacityInUse + CapacityReserved + bundle.BundleWeight <= Capacity);
        }

        public bool Fits(ItemBundle bundle)
        {
            return CapacityInUse + bundle.BundleWeight <= Capacity;
        }
    }
}
