# Linked settlement storage

## Purpose

Settlement management should present one useful inventory view instead of forcing the player to inspect every container individually. Physical storage units remain placeable access points, but compatible units participate in a shared settlement storage network.

## Storage network rules

- Every storage unit has a stable ID, settlement ID, storage family, and tier.
- Units with the same settlement, family, and tier share one logical inventory network. A tier-1 wood stockpile and a tier-1 food store do not merge; two tier-1 general stores do.
- The network aggregates quantities by resource ID. The settlement view therefore reports entries such as `wood 200`, `food 200`, and `water 1000`.
- A resource exists in exactly one authoritative network/container accounting path. Aggregation is a projection over linked units, not duplicated inventory.
- Physical units still matter for placement, access, capacity, ownership metadata, production adjacency, hauling distance, and destruction. Removing or disabling a unit removes only its capacity and contents from the network after an explicit spill/transfer rule; it does not silently delete resources.
- A unit may be linked or unlinked. Unlinked units remain local containers and are useful for private, faction-owned, restricted, or future logistics rules.
- Capacity is summed across linked units. Reservations claim network quantities or capacity atomically and identify the participating unit(s) used for the transfer.

## Production buildings

Production buildings such as mills, butcher tables, farms, kitchens, smelters, and forges may reference a linked storage network for inputs and outputs. Their menus show the network totals relevant to the selected recipe, while the operation still validates:

- the building is active and accessible;
- the actor or job has the required capability;
- all inputs are available in the linked network;
- output capacity exists before work begins;
- the operation can reserve and transfer inputs atomically.

A production building does not create a private duplicate inventory unless its definition explicitly requires a local buffer. Local buffers are visible as a separate component and are included in the settlement aggregate only once.

## Settlement management view

The default settlement storage panel groups by resource ID and reports:

- total available quantity;
- quantity reserved by active jobs or production;
- quantity in transit or held in local buffers;
- total and available capacity;
- contributing storage units and their tiers when detail is requested.

The summary is an accounting projection. Opening the detail view can still answer where the items physically are and which unit or production building is reserving them.

## First implementation slice

Keep the first implementation narrow:

1. Add a validated settlement storage-network identity to existing stockpiles and workstation/farm storage.
2. Aggregate two identical tier-1 storage units by resource ID in the settlement view.
3. Route one workstation recipe through the linked network with atomic reservation and output-capacity checks.
4. Persist network membership and migrate existing containers into a default network without changing their totals.
5. Test removal, duplicate membership, incompatible tiers, reservations, save/load, and malformed-network rollback.

Do not add automatic hauling across the entire settlement, arbitrary cross-settlement sharing, ownership enforcement, remote world storage, or a global inventory. Those require separate issues and explicit rules.
