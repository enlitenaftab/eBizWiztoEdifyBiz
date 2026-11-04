# 02 — Product

| | |
|---|---|
| **Client screen** | eBizWiz → `Master > Item` (Item Master) |
| **Our screen** | EdifyBiz → `Product` |
| **Source tables** | `mstitems` (item), `msditemsuppliers` (suppliers) |
| **Target tables** | `product`, `contprod`, `prodbrandrelation` / `prodbrandmodel` / `prodbatch` |
| **Code** | `Program.cs` → `static class Product` |

One Item Master row becomes one `product` row, one default batch chain, and one `contprod` row per supplier.

---

## 1. Migrated

### 1.1 Item Master → `product`  (main form; Profile and Remark are tabs)

| # | Client UI | Client table.column | Our UI | Our table.column | Note |
|---|---|---|---|---|---|
| 1 | Name | `mstitems.vname` | Name | `product.name` | Capped 150. Item with no name is skipped |
| 2 | Item Code | `mstitems.vitemcode` | CAS# / SKU# / Part# | `product.casno` | Capped 50 |
| 3 | Additional Description | `mstitems.vprintdescription` | Profile tab → Profile | `product.profile` | |
| 4 | Item Type | `mstitems.nitemtype` → `mstitemtype.vname` | Product Sub Category | `product.prodsubcat` | Seeded into `prodcat` as type `'S'` |
| 5 | Item Type (labour flag) | `mstitemtype.blabour` | Product Type | `product.type` | `blabour = 1` → `s` (Service), else `p` (Product) |
| 6 | Item Category | `mstitems.nitemcategory` → `mstitemcategory.vname` | Product Category | `product.prodcat` | Seeded into `prodcat` as type `'M'` |
| 7 | Currency | `mstitems.ncurrency` → `mstcurrency` | Currency | `product.currency` | Seeded into `currency` |
| 8 | Units | `mstitems.nunits` → `mstunits.vname` | Unit | `product.unitcode` | Seeded into `unit` |
| 9 | Party | `mstitems.nparty` → `contact` | Principal Name | `product.ccode` | Resolved through the migrated Contact |
| 10 | Selling Price | `mstitems.nsellingprice` | Product Price | `product.price` | |
| 11 | Purchase Price | `mstitems.npurchaseprice` | Purchase Price | `product.minimumprice` | Label is "Minimum Price" for other companies |
| 12 | Class | `mstitems.nclass` → `mstfixedselection.vdisplayvalue` | Class | `product.grade` | Name stored, not code |
| 13 | Remarks | `mstitems.vremarks` | Remark | `product.remark` | |
| 14 | Active | `mstitems.bactive` | Active | `product.isdeleted` | Inverted |
| 15 | (record office) | `mstitems.nofficeid` | Product Access | `product.companybranch` | Office → `companyaddress` branch |
| 16 | — | fixed value `1` | UOM | `product.baseqty` | EdifyBiz needs a base quantity |
| 17 | (audit) | `addedon` / `addedby` | Created On / By | `product.createdon` / `createdby` | |
| 18 | (audit) | `editedon` / `editedby` | Updated On / By | `product.updatedon` / `updatedby` | Falls back to added on / by |

### 1.2 Item suppliers → `contprod`  (Related Contact tab)

| # | Client UI | Client table.column | Our UI | Our table.column | Note |
|---|---|---|---|---|---|
| 1 | Suppliers of the item | `msditemsuppliers.nparty` → `contact` | Related Contact tab → Supplier | `contprod.contactcode`, `type = 'S'` | No office filter on the link; scope comes from item and party resolution. A supplier that equals the Principal is not repeated |

The Principal (`mstitems.nparty`) stays in `product.ccode` only — it is **not** written as a supplier.

### 1.3 Default batch chain  (Inventory tab)

eBizWiz has no equivalent. EdifyBiz needs it: without a batch the product does not appear in the Sales Invoice product search, and invoice / PO lines have no batch.

| # | Target | Value |
|---|---|---|
| 1 | `prodbrand` | `Default Brand` — shared, created once |
| 2 | `prodmodel` | `default` — shared, created once |
| 3 | `prodbrandrelation` | product ↔ Default Brand |
| 4 | `prodbrandmodel` | relation ↔ default model |
| 5 | `prodbatch` | `Default Batch`, rate 0, opening balance 0 |

### 1.4 Masters seeded

| Source | Target |
|---|---|
| `mstitemcategory` | `prodcat` type `'M'` (Product Category) |
| `mstitemtype` | `prodcat` type `'S'` (Product Sub Category) |
| `mstunits` | `unit` |
| `mstcurrency` | `currency` — active ones, plus inactive ones still used by an item, quotation or order |

---

## 2. Folded into a text field

All four land in `product.remark`, appended after the source remark and separated by ` | `.

| # | Client UI | Client table.column | Written as |
|---|---|---|---|
| 1 | Warranty Months | `mstitems.nwarrantyperiod` | `Warranty(months): <value>` — only when greater than 0 |
| 2 | Maintenance Visit | `mstitems.nnpmvisits` | `Maintenance Visits: <value>` — only when greater than 0 |
| 3 | Contract Amount 1 – 4 | `ncontractamt1` … `ncontractamt4` | `Contract Amt 1-4: <a>/<b>/<c>/<d>` — only when at least one is greater than 0 |

---

## 3. Skipped

| # | Client table.column | On client screen | Why |
|---|---|---|---|
| 1 | `ntechnicalset` | yes — Technical Set | Technical specification sets are not migrated |
| 2 | — | yes — Party Type | Screen-only filter for the Party lookup; eBizWiz does not store it on the item |
| 3 | `bincludeinstock` | no | Stock inclusion flag, no EdifyBiz equivalent |
| 4 | `nminimumstklevel`, `nmaximumstklevel`, `nreorderlevel` | no | Stock levels; EdifyBiz holds reorder level on the brand-model, not the product |
| 5 | `nminimumorderqty`, `nmaximumorderqty` | no | Order quantity limits, no equivalent |
| 6 | `noemwarrperiod` | no | OEM warranty period, no equivalent |
| 7 | `ncheckset` | no | Check-list set; item-level check lists are not migrated |
| 8 | `niccode`, `nsccode` | no | Inspection / service charge codes |
| 9 | `ninspectioncharges`, `nservicecharges` | no | Charge amounts, no equivalent |
| 10 | `nwac` | no | Weighted average cost; EdifyBiz recalculates from stock |
| 11 | `bsysrow`, `bunstable` | no | Internal flags |
| 12 | `ipaddress` | no | Audit only |
| 13 | `Axaptacode`, `acode` | no | External ERP keys |
| 14 | `msditemsuppliers.nlastrate`, `dlastratedate` | no | Supplier last rate and its date (7,734 rows, all filled). `contprod` holds only product, contact and type — it has no rate column |
