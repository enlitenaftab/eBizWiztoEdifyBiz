# 02. Item Master → Product

Format: see `00_MAPPING_FORMAT.md`.

## 1. Overview

| | Client (eBizWiz) | Ours (EdifyBiz) |
|---|---|---|
| Screen / menu | **Master → Item** — *Item Master* | **Product** (add/edit form, view page, Supplier tab) |
| Tables | `mstitems` (item), `msditemsuppliers` (item ↔ supplier); masters `mstitemtype`, `mstitemcategory`, `mstunits`, `mstcurrency`, `mstfixedselection` (class), `mstparty` | `product`, `contprod` (type `S` = supplier), `labelrelation` (item type label), inventory chain `prodbrandrelation` → `prodbrandmodel` → `prodbatch`; masters `label` (category `PRO`), `prodcat`, `unit`, `currency`, `miscellaneous` (Product / Class), `prodbrand`, `prodmodel` |
| Code | — | form + view `product/default.asp`, JS `assets/scripts/product.js`, backend `app/product.asp`, Class dropdown helper `app/UIFunctions.asp` |
| Exe | — | module **2. Product** — class `Product` in `Program.cs` (needs module 1 for the principal / supplier contacts) |
| Scope | offices 2, 3, 4, 6 (1 and 5 are WinMax test offices) | |
| Status | | migrated and verified |

---

## 2. Field mapping

### 2.1 Item Master → `product`

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 1 | Item Code | `mstitems.vitemcode` | `product.casno` | CAS# / SKU# / Part# | CAS# / SKU# / Part# | cap 50 |
| 2 | Name | `vname` | `product.name` | Name | page title | cap 150 |
| 3 | Additional Description | `vprintdescription` | `product.profile` | Profile | — (profile block gated to lopa) | |
| 4 | Item Type | `nitemtype` → `mstitemtype.vname` | `labelrelation` (label `PRO` named after the item type; `module` = product code) | Labels | Labels (product list column + view) | one label per item type; `product.prodtype` is left empty |
| 5 | (Item Type is Labour) | `mstitemtype.blabour` | `product.type` | Product Type | Product Type | LABOUR → `s` (service), everything else → `p` (product) |
| 6 | Item Category | `nitemcategory` → `mstitemcategory.vname` | `product.prodcat` | Product Category | Category | master matched by name, seeded if missing |
| 7 | Currency | `ncurrency` → `mstcurrency` | `product.currency` | Currency | Product Currency | matched by name / short name, seeded if missing |
| 8 | Units | `nunits` → `mstunits.vname` | `product.unitcode` | — (Dimensions → Unit) | UOM (Dimensions section) | master matched by name, seeded if missing |
| 9 | Party | `nparty` → `mstparty` | `product.ccode` | Principal Name | Principal Name | party → contact (module 1); never repeated as a supplier |
| 10 | Party Type | — (screen filter for the Party lookup; not stored) | — | — | — | |
| 11 | Purchase Price | `npurchaseprice` | `product.minimumprice` | **Purchase Price** (label) | **Purchase Price** (label) | |
| 12 | Selling Price | `nsellingprice` | `product.price` | Product Price | — (view gated to Cona) | |
| 13 | Class | `nclass` → `mstfixedselection` (A / B / C) | `product.grade` | **Class** ✚ | **Class** ✚ | name as text |
| 14 | Remarks | `vremarks` | `product.remark` | Remark | Remark | + Warranty / Maintenance Visit appended (rows 15-16) |
| 15 | Warranty Months | `nwarrantyperiod` | part of `product.remark` | Remark | Remark | `Warranty(months): 12` |
| 16 | Maintenance Visit | `nnpmvisits` | part of `product.remark` | Remark | Remark | `Maintenance Visits: 3` |
| 17 | Contract Amount 1 … 4 | `ncontractamt1` … `ncontractamt4` | part of `product.remark` | Remark | Remark | `Contract Amt 1-4: a/b/c/d` — written only when one is filled (none is, §6) |
| 18 | Active | `bactive` | `product.isdeleted` | — | — | inverted: active 1 → `isdeleted` 0 |
| 19 | (office of the record) | `nofficeid` → `mstoffice` | `product.companybranch` | — | — | office → `companyaddress.code` |
| 20 | — | — | `product.baseqty` | — | — | 1 |
| 21 | — | `addedon` / `editedon`, `addedby` / `editedby` | `createdon` / `updatedon`, `createdby` / `updatedby` | — | Created By / Updated By (product view + list) | all 29,966 items resolve to a `mstusers` user. verified in the DB on 23/09/2026 |

### 2.2 Item suppliers → `contprod` (type `S`)

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 22 | (item's suppliers) | `msditemsuppliers.nparty` | `contprod.ccode`, `contprod.pcode`, `type = 'S'` | Supplier tab | Supplier tab | party → contact; the principal (`nparty` of the item) is skipped; one row per item + supplier |

### 2.3 Inventory chain (created for every product, like the product form does)

| # | Our DB | Value | Why |
|---|---|---|---|
| 23 | `prodbrandrelation` | brand **Default Brand** (shared master, reused) | same chain as `app/product.asp` on save |
| 24 | `prodbrandmodel` | model **`default`**, `rol` = 1 | |
| 25 | `prodbatch` | batch **`Default Batch`** | the Sales Invoice product search lists only products with a batch, and Batch No is required on invoice / PO lines; no opening stock is created |

✚ = field added to our form / view for saksham (§4a).

---

## 3. Masters seeded

| Master | Our table | Rows added | Source |
|---|---|---|---|
| Item Type | `label` (category `PRO`) | 19 | every active `mstitemtype` (all 19 are active) + any inactive one used by an item; 12 of them are used by items |
| Item Category | `prodcat` | 1,125 | `mstitemcategory` |
| Units | `unit` | 5 (6 mapped) | `mstunits` |
| Currency | `currency` | 8 (15 mapped) | `mstcurrency` |
| Class | `miscellaneous` (Product / Class) | 1 | `mstfixedselection` values used by items |
| Default Brand / model `default` | `prodbrand` / `prodmodel` | reused (codes 1 / 2) | created only if missing |

---

## 4. Changes made on our side

### 4a. Form / view changes (saksham-gated)

| # | Screen | Change | Kind | File | DB column |
|---|---|---|---|---|---|
| 1 | Product form | Label "Minimum Price" → **Purchase Price** | Label | `product/default.asp` (`lblPrice`) | `product.minimumprice` |
| 2 | Product form | **Class** dropdown + quick-add (+) | New field | `product/default.asp` (saksham row after prices) | `product.grade` |
| 3 | Product view | Label "Minimum Price" → **Purchase Price** | Label | `product/default.asp` (view prices) | `product.minimumprice` |
| 4 | Product view | New row **Class** | New field | `product/default.asp` (saksham block) | `product.grade` |
| 5 | Product form (edit) | Class dropdown filled from the product array (`aProduct[56]` = `grade`, already returned by the backend) | Behaviour | `assets/scripts/product.js` | `product.grade` |
| 6 | Dropdown helper | `productClassSelect` (used grades ∪ `miscellaneous` Product / Class) | Backend | `app/UIFunctions.asp` | — |

**New fields on the form: 1** (Class) · **on the view: 1** (Class) · labels: 2 · behaviour / backend: 2.

### 4b. Database changes (ALTER)

None.

---

## 5. Not migrated

| Client UI | Client DB | Rows | Why |
|---|---|---|---|
| Technical Set | `mstitems.ntechnicalset` | 1 item | no matching field on our product |
| Party Type | — | — | screen filter only, nothing stored |
| Supplier links to a test-office party | `msditemsuppliers` | 127 links | the supplier party is out of scope |
| Supplier = the item's own principal | `msditemsuppliers` | 6,101 rows | the principal is already `product.ccode` |
| Item Type: Warranty Item flag | `mstitemtype.bwarranty` | 3 types (Equipment, MODELS, Office Equipments) | a Product label has no such flag; warranty per serial comes with the AMC / warranty contracts (module 10) |
| Item Category: Remarks | `mstitemcategory.vremarks` | 1 | our Product Category has no remark |
| Minimum Order Qty | `mstitems.nminimumorderqty` | 1,712 items, all = 1 | the default value; no such field on our product |
| Bin No. (office-wise) | `mststkdt.vbinno` | 41 | no bin / rack field on our product or batch |
| Office-wise stock levels (Change Item Levels) | `mststkdt` min / max / reorder / order qty | 0 filled | nothing to migrate |
| Purchase / selling price change log | `traupdtpurchsellpricemstitems` | 198 | history only; the current prices are on the product |
| Item documents (More > Upload Doc.) | `trhdoc` | — | DMS is out of scope |
| Stock levels / reorder / OEM warranty / check set / WAC … | `nminimumstklevel`, `nmaximumstklevel`, `nreorderlevel`, `nminimumorderqty`, `nmaximumorderqty`, `noemwarrperiod`, `ncheckset`, `niccode`, `nsccode`, `ninspectioncharges`, `nservicecharges`, `nwac`, `bincludeinstock`, `bunstable` | — | not on the client's Item Master screen; not migrated |

---

## 6. Verification (latest run — 0 errors)

| Check | Client | Ours |
|---|---|---|
| Items → products | 29,966 (0 without a name) | **29,966** migrated (`product` 29,967 incl. the template "Dummy Product") |
| Principal (`nparty`) | 29,614 items with a party | **29,614** resolved, 0 unresolved |
| Product type | 28 items of a LABOUR type | `type = 's'` on **28** |
| Item Type labels | 29,966 items, 12 item types in use (Accessories 14,426 · Equipment 10,352 · SPARES 3,625 · Sea Freight 812 · Forwarding Charges 495 · Freight & Insurance Charges 194 · LABOUR 28 · Office Equipments 14 · Crate Fumigation Charges 9 · Calibration Instrument 9 · MODELS 1 · Freight Charges 1) | **29,966** products carry exactly one `PRO` label (all 12 counts match); `product.prodtype` empty on every row, `prod_type` has 0 rows |
| Class | — | `grade` set on 14,097 products |
| Warranty / Maintenance visits | 9,314 / 4,894 items | 9,314 remarks carry `Warranty(months)` |
| Contract Amount 1-4 | **0** items with a value | nothing to append |
| Supplier links | 1,359 distinct non-principal item + supplier pairs, 1,232 with a real-office party | **1,232** `contprod` `S` |
| Inventory chain | — | 29,967 `Default Batch` — every product exactly one brand relation → one model → one batch |
| Active | 3 inactive items | 3 products `isdeleted = 1` |

---

## 7. Notes

- **Item → product on every document line is matched by Item Code** (`product.casno` = `vitemcode`, unique per item) + Name, name only as a last resort (`Shared.MapItemsToProducts`). 1,732 client names are shared by 8,423 items (same model name, different Item Code, e.g. 20 codes of "L15/11/B510 Tmax 1100 with Flap Door"); a name-only match would put all their quotation / order / invoice / stock lines on one product.
- **Item Opening Balance** (office-wise, `mststkdt`) is migrated by module 8 as EdifyBiz **Inventory > Opening Stock** — see `10_stock_field_mapping.md` row 37.

- **Item Type is a Product label, not `product.prodtype`.** In EdifyBiz `prod_type` is the tax-type master: `prod_type_tax` holds the taxes of a type, saving a product copies them into `prod_tax` (`app/product.asp:588`) and the view shows the field as "Tax Type". Item Type cannot be the parent Product Category either — 253 of the 1,072 client item categories sit under more than one item type. Labels (category `PRO`) are listed in the product list and on the view, and can be filtered with the Labels button.
- **Selling Price** is stored in `product.price`; saksham sees it on the form ("Product Price") but the view shows the price for Cona only.
- **Additional Description** (`profile`) is editable on the form, the view shows the profile block for lopa only.
- **Units** are set through the Dimensions section (UOM), not on the main form.
- Warranty months and maintenance visits have no product column, so they are kept as labelled text in Remark; the AMC contract module carries the real warranty / PM data per serial.
- The exe console line "contprod rows (Principal + Suppliers)" counts suppliers only — the principal is never written to `contprod`.
- `Default Batch` exists so migrated products behave like form-created ones in Sales Invoice / Purchase Order; the batch holds no opening stock (stock comes from module 8).

