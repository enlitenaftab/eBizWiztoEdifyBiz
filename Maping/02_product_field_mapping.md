# Product — Migration Mapping (eBizWiz → EdifyBiz)

Client module **Item Master** (Product). Read as: what the client sees → where it sits in the client DB → which column it goes to in our DB → how it shows in our UI.

Source tables: `mstitems` (item), `msditemsuppliers` (item suppliers).
Target tables: `product` (item), `contprod` (item ↔ contact links: Principal / Suppliers).
Scope: offices 2 (HO Mumbai), 3 (Bangalore), 4 (Delhi), 6 (Kolkata). Test offices 1 & 5 excluded.

---

## 1) Item Master → `product` (+ `contprod`)

| Client UI (eBizWiz) | Client DB (`mstitems`) | Our DB (target column) | Our UI (EdifyBiz) |
|---|---|---|---|
| Item Code | `vitemcode` | `product.casno` | Item Code |
| Name | `vname` | `product.name` | Name |
| Additional Description | `vprintdescription` | `product.profile` | Additional Description |
| Item Type | `nitemtype` → `mstitemtype.vname` | `product.prodtype` (+ `product.type` = 'p' product / 's' service) | Item Type |
| Item Category | `nitemcategory` → `mstitemcategory.vname` | `product.prodcat` | Item Category |
| Currency | `ncurrency` → `mstcurrency` (name/short name) | `product.currency` | Currency |
| Units | `nunits` → `mstunits.vname` | `product.unitcode` | Units |
| Party | `nparty` → target `contact` (by name) | `product.ccode` | Principal Name |
| Party Type | shows "PRINCIPAL" | (identifies `nparty` as the Principal; no separate column) | Principal (the Party above) |
| Purchase Price | `npurchaseprice` | `product.minimumprice` | Purchase Price |
| Selling Price | `nsellingprice` | `product.price` | Selling Price |
| Class | `nclass` → `mstfixedselection` (A/B/C) | `product.grade` | Class |
| Remarks | `vremarks` | `product.remark` | Remarks |
| Active | `bactive` | `product.isdeleted` | Active (inverted: active=1 → isdeleted=0) |
| (office of the record) | `nofficeid` | `product.companybranch` | company branch (per office) |
| Suppliers | `msditemsuppliers` → target `contact` (by name) | `contprod` rows, type = 'S' | Suppliers list (Principal is NOT repeated here) |

Notes:
- Currency/Item Type/Item Category/Units: matched to the target master by name; the master row is created if genuinely missing.
- Principal (`nparty`) lives in `product.ccode` only — it is never added to `contprod` as a supplier.

---

## 2) Fields folded into Remarks (no dedicated target column)

These client fields have no matching column in `product`, so they are appended (labelled) into `product.remark` — nothing is lost:

| Client UI (eBizWiz) | Client DB (`mstitems`) | Stored in |
|---|---|---|
| Warranty Months | `nwarrantyperiod` | `product.remark` (labelled) |
| Maintenance Visit | `nnpmvisits` | `product.remark` (labelled) |
| Contract Amount 1 | `ncontractamt1` | `product.remark` (labelled) |
| Contract Amount 2 | `ncontractamt2` | `product.remark` (labelled) |
| Contract Amount 3 | `ncontractamt3` | `product.remark` (labelled) |
| Contract Amount 4 | `ncontractamt4` | `product.remark` (labelled) |

---

## 3) Not migrated (client field with no target home)

| Client UI | Client DB | Reason |
|---|---|---|
| Technical Set | `ntechnicalset` | no matching field in `product`; not required |

---

## Inventory chain — Default Brand / Model / Batch (added 2026-09-15)

Every migrated product also gets the same chain the product form creates (`app/product.asp`):
`product` → `prodbrandrelation` (**Default Brand**) → `prodbrandmodel` (model **`default`**, `rol` = 1) → `prodbatch` (**`Default Batch`**).

- Default Brand and model `default` are shared masters — reused if present (saksham target already has them: brand code 1, model code 2), created only if missing.
- Any part of the chain that already exists for a product is reused, never duplicated.
- Why: the Sales Invoice product search (`selectjson` `ProductInvoice`) lists only products that have a batch, and Batch No is required on invoice lines. No opening stock (`stocktrans`) is created.
- Line batch: Sales Invoice (`sal_order_det.prodbatchcode`) and Purchase Order (`pur_order_det.prodbatchcode`) lines get the product's Default Batch. Inquiry / Quotation / Sales Order lines (`inqcsdet`) have no batch column — no change.

_Verified 2026-09-15 (full run): 29,967 products (29,966 migrated + Dummy Product) → each exactly 1 relation (brand 1) → 1 brandmodel (model 2, rol 1) → 1 `Default Batch`; 0 missing, 0 duplicates, 0 orphans; same shape as the form-created Dummy Product (unit NULL, rate/opening/damaged 0). Invoice product search shows all 29,964 active products._
