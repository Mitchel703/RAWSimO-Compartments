﻿using RAWSimO.Core.Geometrics;
using RAWSimO.Core.Helper;
using RAWSimO.Core.Info;
using RAWSimO.Core.IO;
using RAWSimO.Core.Items;
using RAWSimO.Core.Management;
using RAWSimO.Core.Waypoints;
using RAWSimO.Toolbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace RAWSimO.Core.Elements
{
    /// <summary>
    /// Implements a pod that offers storage space.
    /// </summary>
    public class Pod : Circle, IPodInfo, IExposeVolatileID
    {
        #region Constructors

        /// <summary>
        /// Creates a new pod.
        /// </summary>
        /// <param name="instance">The instance this pod belongs to.</param>
        internal Pod(Instance instance) : base(instance)
        {
            this.instance = instance;
        }

        #endregion

        #region Core

        /// <summary>
        /// The default pod orientation to use when setting down a pod.
        /// </summary>
        internal const double DEFAULT_BUCKET_ORIENTATION = 0;

        /// <summary>
        /// The maximal number of SKUs that will be stored using fast access implementations.
        /// </summary>
        private readonly Instance instance;

        ///// <summary>
        ///// The capacity of this pod.
        ///// </summary>
        //internal double Capacity;
        internal List<Compartment> Compartments;

        /// <summary>
        /// Indicates whether the pod is currently carried by a bot.
        /// </summary>
        internal bool InUse;

        /// <summary>
        /// The waypoint this pod is currently stored at. If the pod is currently being used or not properly setdown at a waypoint, this field will be <code>null</code>.
        /// </summary>
        internal Waypoint Waypoint;

        /// <summary>
        /// The bot this waypoint is currently carried by. <code>null</code>, if currently not carried.
        /// </summary>
        internal Bot Bot;

        ///// <summary>
        /// All items that are physically contained in this pod.
        /// </summary>
        internal IEnumerable<ItemDescription> ItemDescriptionsContained { get { return Compartments.SelectMany(x => x._itemDescriptionsContained); } }
        /// <summary>
        /// All extract requests that shall be completed with this pod.
        /// </summary>
        private HashSet<ExtractRequest> _extractRequestsRegistered = new HashSet<ExtractRequest>();
        /// <summary>
        /// Initializes the content info of this pod. Call this only once before something is added to the pod.
        /// </summary>
        private void InitPodContentInfo()
        {
            foreach (var c in Compartments)
            {
                c.InitCompartmentContentInfo();
            }
        }

        /// <summary>
        /// Reserves capacity of this pod for the given bundle. The reserved capacity will be maintained when the bundle is allocated.
        /// </summary>
        /// <param name="bundle">The bundle for which capacity shall be reserved.</param>
        internal void RegisterBundle(ItemBundle bundle, Compartment chosenCompartment)
        {
            chosenCompartment.RegisterBundle(bundle);
            // Notify the instance about the reservation
            Instance.NotifyBundleRegistered(this, bundle);
        }

        Dictionary<ItemDescription, Queue<Compartment>> registrations = new Dictionary<Items.ItemDescription, Queue<Compartment>>();

        /// <summary>
        /// Reserves an item that is going to be picked at a station.
        /// </summary>
        /// <param name="item">The item that is going to be reserved for picking.</param>
        /// <param name="extractRequest">The request for which the item shall be reserved.</param>
        internal void RegisterItem(ItemDescription item, ExtractRequest extractRequest)
        {
            var availableCompartments = Compartments.Where(x => x.IsContained(item));
            var choosenCompartment = availableCompartments
                .OrderBy(p => instance.Randomizer.NextDouble())
                .First();

            choosenCompartment.RegisterItem(item, extractRequest);

            _extractRequestsRegistered.Add(extractRequest);
            extractRequest.Assign(this);

            if (!registrations.ContainsKey(item))
            {
                registrations.Add(item, new Queue<Compartment>());
            }

            registrations[item].Enqueue(choosenCompartment);

            // Notify instance
            Instance.NotifyPodItemReserved(this, item, extractRequest);
        }
        /// <summary>
        /// Revokes a reservation of an item.
        /// </summary>
        /// <param name="item">The type of the item.</param>
        /// <param name="extractRequest">The request which should have been done with the item.</param>
        internal void UnregisterItem(ItemDescription item, ExtractRequest extractRequest)
        {
            //_itemDescriptionCountAvailable[item]++;
            var choosenCompartment = registrations[item].Dequeue();
            choosenCompartment.UnregisterItem(item, extractRequest);

            _extractRequestsRegistered.Remove(extractRequest);
            extractRequest.Unassign(this);
            // Notify instance
            Instance.NotifyPodItemUnreserved(this, item, extractRequest);
        }

        /// <summary>
        /// Adds the specified bundle of item to the pod.
        /// </summary>
        /// <param name="itemBundle">The item-bundle to add.</param>
        /// <param name="insertRequest">The corresponding insertion request, if available.</param>
        /// <returns><code>true</code> if the item was added successfully, <code>false</code> otherwise.</returns>
        public bool Add(Compartment choosenCompartment, ItemBundle itemBundle, InsertRequest insertRequest = null)
        {
            // Signal change
            _changed = true;
            // Only add the item, if there is enough remaining capacity
            if (choosenCompartment.CapacityInUse + itemBundle.BundleWeight <= choosenCompartment.Capacity)
            {
                // Prepare info for the interface
                _contentChanged = true;
                choosenCompartment.Add(itemBundle, insertRequest);

                // Mark insert request completed, if there is one
                insertRequest?.Finish();
                // Signal the success
                return true;
            }
            else
            {
                // Mark insert request aborted, if there is one
                insertRequest?.Abort();
                // Signal the failed add operation
                return false;
            }
        }

        /// <summary>
        /// Removes the item from the pod.
        /// </summary>
        /// <param name="item">The item to remove.</param>
        /// <param name="extractRequest">The corresponding extract request.</param>
        public void Remove(ItemDescription item, ExtractRequest extractRequest)
        {
            //TODO: to be known before which compartment was requested
            var availableCompartments = Compartments.Where(x => x.IsContained(item));
            var choosenCompartment = availableCompartments
                .OrderBy(p => instance.Randomizer.NextDouble())
                .First();
            choosenCompartment.Remove(item, extractRequest);
            // Signal change
            _changed = true;
            // Mark extract request completed, if there is one
            extractRequest?.Finish();

            // Notify the instance about the removed item
            Instance.NotifyItemExtracted(this, item);
            // Prepare info for the interface
            _contentChanged = true;
        }

        /// <summary>
        /// Checks whether an item matching the description is contained in this pod.
        /// </summary>
        /// <param name="itemDescription">The description to check.</param>
        /// <returns><code>true</code> if such an item is present, <code>false</code> otherwise.</returns>
        public bool IsContained(ItemDescription itemDescription) { return Compartments.Any(x => x.IsContained(itemDescription)); }
        /// <summary>
        /// Checks whether an item matching the description is available in this pod.
        /// </summary>
        /// <param name="itemDescription">The description to check.</param>
        /// <returns><code>true</code> if such an item is available, <code>false</code> otherwise.</returns>
        public bool IsAvailable(ItemDescription itemDescription) { return Compartments.Any(x => x.IsAvailable(itemDescription)); }

        /// <summary>
        /// Returns how many items of the specified type are contained in this pod.
        /// </summary>
        /// <param name="itemDescription">The item to check for.</param>
        /// <returns>The count of items of the specified type that are contained in this pod.</returns>
        public int CountContained(ItemDescription itemDescription) { return Compartments.Sum(x => x.CountContained(itemDescription)); }

        /// <summary>
        /// Returns how many items of the specified type are still available in this pod (not already reserved for picking).
        /// </summary>
        /// <param name="itemDescription">The item to check for.</param>
        /// <returns>The count of items of the specified type that are still available in this pod.</returns>
        public int CountAvailable(ItemDescription itemDescription) { return Compartments.Sum(x => x.CountAvailable(itemDescription)); }

        /// <summary>
        /// Checks whether the specified bundle of items fits into this pod.
        /// </summary>
        /// <param name="bundle">The item-bundle to check.</param>
        /// <returns><code>true</code> if the items fit this pod, <code>false</code> otherwise.</returns>
        public bool Fits(ItemBundle bundle)
        {
            return Compartments.Any(c => c.Fits(bundle));
        }

        /// <summary>
        /// Checks whether the specified bundle of items can be added for reservation to this pod.
        /// </summary>
        /// <param name="bundle">The bundle that has to be checked.</param>
        /// <returns><code>true</code> if the bundle fits, <code>false</code> otherwise.</returns>
        public List<Compartment> CompartmentFits(ItemBundle bundle)
        {
            return Compartments.Where(c => c.Fits(bundle)).ToList();
        }

        public List<Compartment> CompartmentFitsForReservation(ItemBundle bundle)
        {
            return this.Compartments
                .Where(compartment => compartment.FitsForReservation(bundle))
                .ToList();
        }

        #endregion

        #region Statistics

        /// <summary>
        /// The number of items given to output-stations.
        /// </summary>
        public int StatItemsHandled;

        /// <summary>
        /// The number of bundles received from input-stations.
        /// </summary>
        public int StatBundlesHandled;

        /// <summary>
        /// Resets the statistics.
        /// </summary>
        public void ResetStatistics()
        {
            StatItemsHandled = 0;
            StatBundlesHandled = 0;
        }

        #endregion

        #region Inherited methods

        /// <summary>
        /// Returns a simple string identifying this object in its instance.
        /// </summary>
        /// <returns>A simple name identifying the instance element.</returns>
        public override string GetIdentfierString() { return "Pod" + this.ID; }
        /// <summary>
        /// Returns a simple string giving information about the object.
        /// </summary>
        /// <returns>A simple string.</returns>
        public override string ToString()
        {
            var msg = "Pod" + ID + "[";
            foreach (var c in Compartments)
            {
                msg += "(";
                msg += (c.CapacityInUse / c.Capacity).ToString(IOConstants.EXPORT_FORMAT_SHORT, IOConstants.FORMATTER) + "%";
                msg += ((c.CapacityInUse + c.CapacityReserved) / c.Capacity).ToString(IOConstants.EXPORT_FORMAT_SHORT, IOConstants.FORMATTER) + "%";
                msg += ")";
            }
            msg += "]";
            return msg;
        }

        #endregion

        #region IPodInfo Members

        /// <summary>
        /// A number distinguishing the type of the pod.
        /// </summary>
        public double InfoTagPodStorageType { get; internal set; }
        /// <summary>
        /// Some additional information about the pod.
        /// </summary>
        public string InfoTagPodStorageInfo { get; internal set; }
        /// <summary>
        /// If a pod utility manager is active, this field contains the calculated speed of the pod.
        /// </summary>
        internal double InfoTagPodSpeed
        {
            get
            {
                double value = Instance.ElementMetaInfoTracker != null && Instance.ElementMetaInfoTracker.PodSpeedMax > 0 ?
                    Instance.ElementMetaInfoTracker.GetPodSpeed(this) / Instance.ElementMetaInfoTracker.PodSpeedMax :
                    0;
                return Math.Min(value, 1);
            }
        }
        /// <summary>
        /// If a pod utility manager is active, this field contains the calculated utility of the pod.
        /// </summary>
        internal double InfoTagPodUtility
        {
            get
            {
                double value = Instance.ElementMetaInfoTracker != null && Instance.ElementMetaInfoTracker.PodUtilityMax > 0 ?
                    Instance.ElementMetaInfoTracker.GetPodUtility(this) / Instance.ElementMetaInfoTracker.PodUtilityMax :
                    0;
                return Math.Min(value, 1);
            }
        }
        /// <summary>
        /// If a pod utility manager is active, this field contains the calculated combined speed and utility of the pod.
        /// </summary>
        internal double InfoTagPodCombined
        {
            get
            {
                double value = Instance.ElementMetaInfoTracker != null ? Instance.ElementMetaInfoTracker.GetPodCombinedScore(this, 1, 1) : 0;
                return Math.Min(value, 1);
            }
        }

        /// <summary>
        /// Gets the current heat associated with this pod as a value between 0 (low heat) and 100 (high heat).
        /// </summary>
        /// <returns>The heat of this pod.</returns>
        public double GetInfoHeatValue()
        {
            switch (Instance.SettingConfig.HeatMode)
            {
                case HeatMode.NumItemsHandled:
                    return Instance.StatMaxItemsHandledByPod > 0 ? (double)StatItemsHandled / (double)Instance.StatMaxItemsHandledByPod : 0;
                case HeatMode.NumBundlesHandled:
                    return Instance.StatMaxBundlesHandledByPod > 0 ? (double)StatBundlesHandled / (double)Instance.StatMaxBundlesHandledByPod : 0;
                case HeatMode.CurrentCapacityUtilization:
                    return Compartments.Sum(c => c.CapacityInUse) / Compartments.Sum(c => c.Capacity);
                case HeatMode.AverageFrequency:
                //{
                //    if (_itemDescriptionCountContained == null || !Instance.ItemDescriptions.Any(item => _itemDescriptionCountContained[item] > 0))
                //        return 0;
                //    else
                //    {
                //        int contained = 0; double value = 0;
                //        foreach (var item in Instance.ItemDescriptions)
                //        {
                //            if (_itemDescriptionCountContained[item] > 0)
                //            {
                //                contained++;
                //                value += Instance.FrequencyTracker.GetMeasuredFrequency(item);
                //            }
                //        }
                //        return contained > 0 ? value / contained : 0;
                //    }

                //}
                case HeatMode.MaxFrequency:
                //{
                //    if (_itemDescriptionCountContained == null || !Instance.ItemDescriptions.Any(item => _itemDescriptionCountContained[item] > 0))
                //        return 0;
                //    else
                //    {
                //        int contained = 0; double value = 0;
                //        foreach (var item in Instance.ItemDescriptions)
                //        {
                //            if (_itemDescriptionCountContained[item] > 0)
                //            {
                //                contained++;
                //                value = Math.Max(value, Instance.FrequencyTracker.GetMeasuredFrequency(item));
                //            }
                //        }
                //        return value;
                //    }
                //}
                case HeatMode.AverageStaticFrequency:
                //{
                //    if (_itemDescriptionCountContained == null || !Instance.ItemDescriptions.Any(item => _itemDescriptionCountContained[item] > 0))
                //        return 0;
                //    else
                //    {
                //        int contained = 0; double value = 0;
                //        foreach (var item in Instance.ItemDescriptions)
                //        {
                //            if (_itemDescriptionCountContained[item] > 0)
                //            {
                //                contained++;
                //                value += Instance.FrequencyTracker.GetStaticFrequency(item);
                //            }
                //        }
                //        return contained > 0 ? value / contained : 0;
                //    }
                //}
                case HeatMode.MaxStaticFrequency:
                //{
                //    if (_itemDescriptionCountContained == null || !Instance.ItemDescriptions.Any(item => _itemDescriptionCountContained[item] > 0))
                //        return 0;
                //    else
                //    {
                //        int contained = 0; double value = 0;
                //        foreach (var item in Instance.ItemDescriptions)
                //        {
                //            if (_itemDescriptionCountContained[item] > 0)
                //            {
                //                contained++;
                //                value = Math.Max(value, Instance.FrequencyTracker.GetStaticFrequency(item));
                //            }
                //        }
                //        return value;
                //    }
                //}
                case HeatMode.StorageType:
                    return InfoTagPodStorageType;
                case HeatMode.CacheType:
                    {
                        Waypoint wp = Waypoint;
                        if (wp == null)
                        {
                            return 0;
                        }
                        else
                        {
                            switch (wp.InfoTagCache)
                            {
                                case Control.Shared.ZoneType.None: return 0.25;
                                case Control.Shared.ZoneType.Cache: return 0.75;
                                case Control.Shared.ZoneType.Dropoff: return 1;
                                default: throw new ArgumentException("Unknown storage location type: " + wp.InfoTagCache.ToString());
                            }
                        }
                    }
                case HeatMode.ProminenceValue:
                    {
                        Waypoint wp = Waypoint;
                        if (wp == null) return 0;
                        else return wp.InfoTagProminence;
                    }
                case HeatMode.PodSpeed:
                    {
                        return InfoTagPodSpeed;
                    }
                case HeatMode.PodUtility:
                    {
                        return InfoTagPodUtility;
                    }
                case HeatMode.PodCombinedValue:
                    {
                        return InfoTagPodCombined;
                    }
                default:
                    return 0;
            }
        }
        /// <summary>
        /// Gets the capacity this pod offers.
        /// </summary>
        /// <returns>The capacity of the pod.</returns>
        public double GetInfoCapacity() { return Compartments.Sum(x => x.Capacity); }

        public List<double> GetInfoCompartmentCapacity() { return Compartments.Select(x => x.Capacity).ToList(); }


        /// <summary>
        /// Gets the absolute capacity currently in use.
        /// </summary>
        /// <returns>The capacity in use.</returns>
        public double GetInfoCapacityUsed() { return Compartments.Sum(x => x.CapacityInUse); }

        public List<double> GetInfoCompartmentsCapacityUsed() { return Compartments.Select(x => x.CapacityInUse).ToList(); }

        /// <summary>
        /// Gets the absolute capacity currently reserved.
        /// </summary>
        /// <returns>The capacity reserved.</returns>
        public double GetInfoCapacityReserved() { return Compartments.Sum(x => x.CapacityReserved); }
        /// <summary>
        /// Indicates whether something changed 
        /// </summary>
        private bool _contentChanged = true;
        /// <summary>
        /// Gets information about number of items of the given kind in this pod.
        /// </summary>
        /// <returns>The number of units contained in the pod of the specified item.</returns>
        public int GetInfoContent(IItemDescriptionInfo item) { _contentChanged = false; return Compartments.Sum(x => x.GetInfoContent(item)); }
        /// <summary>
        /// Indicates whether the content of the pod changed.
        /// </summary>
        /// <returns>Indicates whether the content of the pod changed.</returns>
        public bool GetInfoContentChanged() { return _contentChanged; }
        /// <summary>
        /// Indicates whether the pod is ready for refill.
        /// </summary>
        /// <returns>Indicates whether the pod is ready for refill.</returns>
        public bool GetInfoReadyForRefill() { return Instance.Controller.StorageManager.IsAboveRefillThreshold(this); }
        /// <summary>
        /// Gets the current tier this object is placed on. Can't change in case of an immovable object.
        /// </summary>
        /// <returns>The current tier.</returns>
        public ITierInfo GetInfoCurrentTier() { return Tier; }
        /// <summary>
        /// Returns the active instance belonging to this element.
        /// </summary>
        /// <returns>The active instance.</returns>
        public IInstanceInfo GetInfoInstance() { return Instance; }
        /// <summary>
        /// Indicates whether the underlying object changed since the last call of <code>GetChanged()</code>.
        /// </summary>
        /// <returns><code>true</code> if the object changed since the last call of this method, <code>false</code> otherwise.</returns>
        public bool GetInfoChanged() { bool changed = _changed; _changed = false; return changed; }

        #endregion

        #region IExposeVolatileID Members

        /// <summary>
        /// An ID that is useful as an index for listing this item.
        /// This ID is unique among all <code>ItemDescription</code>s while being as low as possible.
        /// Note: For now the volatile ID matches the actual ID.
        /// </summary>
        int IExposeVolatileID.VolatileID { get { return VolatileID; } }

        #endregion
    }
}
