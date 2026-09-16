# Purchase Order — Migration Mapping (eBizWiz → EdifyBiz)

Client module **Order Placed** (Purchase Order). Read as: what the client sees → where it sits in the client DB → which column it goes to in our DB → how it shows in our UI.

EdifyBiz Purchase Order = `pur_order` (`type='PO'`), line items = `pur_order_det` (`purcode` → `pur_order.code`). Live saksham screen = **`gst/purchase/default.asp`** (menu → `/edify/gst/purchase/`). Save = generic `SqlOperation("add"/"edit","pur_order")` (form field `txt<col>` → column).

Source tables: `trhordpl` (header), `trdordpl1items` (line items). Scope: offices 2/3/4/6. ~6,031 orders, ~80,845 line items.

> Order Type is constant **ON PARTY** (all 6,031); Order Placed Type = FRESH 372 / AGAINST 5,659 (~94% against a Sales Order, line-level `nordrcditemdetailncode`).

---

## 1) Order header → `pur_order` (reuse existing columns)

| Client UI (eBizWiz) | Client DB (`trhordpl`) | Our DB (`pur_order`) |
|---|---|---|
| Trn. No. | `vtrnprefix` + `ntrnno` | `purorderno` |
| Trn. Date | `dtrndate` | `purorderdt` |
| Order To (supplier) | `ntoparty` → target `contact` (by name) | `scode` |
| Order Currency | `ncurrency` → `currency` master | `currency` |
| Total Discount % | `nprindiscountperc` | `discount` |
| Dispatch Date | `dprindispatchdate` | `deliverydate` |
| Terms & Cond. (text) | `vtermsconditions` | `pay_notes` |
| Order Status | `nordplcdstatus` → `status` master (`module='Purchase Order'`) | `status` |
| Against Order Received (SO no.) | line `nordrcditemdetailncode` → its Sales Order (read from ALL lines, incl. blank-item lines) | `salesordercode` (comma list of SO `inqcs.code`) |
| Remarks + Comment | `vremarks` + `vcomment` | `remarks` |
| (office) | `nofficeid` | `branchcode` |
| Sales Person | (none in eBizWiz PO) | `executive` = Admin |
| — | — | `comcode` = company, `type` = 'PO' |

Notes:
- **No exchange rate:** `trhordpl` has no exchange-rate column (only `ncurrency`); leave `pur_order.exchangerate` default.
- **`nterms` is NOT the terms text** — it is the PRE/POST flag (`mstfixedselection` `npreorpost`), 93% empty; skip it. Terms text = `vtermsconditions` → `pay_notes`.
- **Order Status:** source has ~14 statuses (FULLFILLED, 1.O/A Awaited … 8.Order Cancelled) + NULL; seed them into the `status` master (`module='Purchase Order'`).

---

## 2) Order header → new `pur_order` columns (guarded ALTER)

Well-populated in source, no existing column — add clean new columns (all lower-case, no clash):

| Client UI (eBizWiz) | Client DB (`trhordpl`) | New `pur_order` column | Coverage |
|---|---|---|---|
| Bill To | `nbillto` (party) → migrated `contact` → its default `mltaddress` code | `cbranchcode` int (existing → mltaddress; the form's Bill To/Supplier-Branch field) | 82% |
| Ship To | `nshipto` (party) → migrated `contact` → its default `mltaddress` code | `shippingbranch` int (existing → mltaddress) | 82% |
| Inquiry Category | `ninquirycategory` → `miscellaneous(Inquiry/Category)` | `inquirycategory` int | 82% |
| Pending Reason | `npendingreason` → **`mstcallpendingreasons.vname`** (47 reasons, "OPD - …") | `pendingreason` int → `miscellaneous` | 72% |
| Invoice No. | `vinvno` | `invoiceno` nvarchar | 52% |
| Invoice Date | `dinvdate` | `invoicedate` datetime | — |
| O/A No. | `vrefno` | `oano` nvarchar | 63% |
| O/A Date | `drefdate` | `oadate` datetime | — |

Notes:
- **Bill To / Ship To = branch reference** (same as Sales Order / Sales Invoice, decided 2026-09-12): source `nbillto`/`nshipto` are party codes → resolve each party → its migrated `contact` → default `mltaddress` code → store in **`cbranchcode` (existing, = Bill To)** / **`shippingbranch` (existing, = Ship To)**. NO new column, NO text. UI: both **free-hand** pickers (`select2json` + `data="freebranch"`) — Bill To = `purchase_form_cbranchcode` (relabelled), Ship To = `purchase_form_shipbranch` beside it.
- **Pending Reason** master is `mstcallpendingreasons` (NOT `mstfixedselection` — that join gives garbage); seed used values into `miscellaneous`.
- **Inquiry Category** uses the same Category master the CI/SO migration already seeds.

---

## 3) Line items → `pur_order_det`

| Client UI | Client DB (`trdordpl1items`) | Our DB (`pur_order_det`) |
|---|---|---|
| Product | `nitem` → target `product` (by name) | `prodcode` |
| Quantity | `nquantity` | `qty` |
| Rate | `nrate` | `price` |
| Discount (amount) | `ndiscount` | not migrated — already inside `nrate` |
| Line Currency | `nitemcurrency` → `currency` master | `currency` |

FK = `pur_order_det.purcode = pur_order.code`. `ndiscountperc` is a UI helper (amount is stored). Per-line tax not migrated.

---

## 4) Not migrated

| Client UI | Client DB | Reason |
|---|---|---|
| Order Type (ON PARTY) | `nordplcdtype` | constant, only 2 values (senior: skip) |
| Order Placed Type (Fresh/Against) | `nordertype` | AGAINST inferred from `salesordercode` presence |
| Installation Date | `dprininstdate` | negligible (33 rows) |
| Commission / OR-Payment / Custom-Clearance / CCS | `nprincomm*` / `nprinorpay*` / `ncustomcle*` / `vccsremarks` | see §6 (client-confirm); sparse |
| Check List | `ncheckset` | replaced by Task Check List tab (§5) |
| Select Letterhead / Trn. Total | `vletterhead` / totals | print-time / derived |
| Payment Schedule + actual payments | `trdordpl3payterms` / `trhpayrg` | see §7 |

---

## 5) Task Check List tab — already live

The standard **Task Check List** tab is **already present and ungated** in `gst/purchase/default.asp` (tab `#tabTaskchecklist`, `TaskCheckListFill.init()`), so it works for saksham with no change. Stored in `taskchecklist` (`module='PUR'` — the tag purchaseorder.js passes; NOT 'TSK', which is the Task module — `modulecode = pur_order.code`); entered live, not migrated. The client `ncheckset` value itself is not migrated.

---

## 6) Commission section — client-confirm (not migrated)

The client PO form's Commission / OR-Value-Payment / Custom-Clearance block (several fields read-only / password-protected) is **not migrated** (sparse). For the form, confirm the read-only/role logic on the client call before finalizing — do not reverse-engineer it.

---

## 7) Payments — not part of PO

- **Payment Schedule** (`payment_revenue`, the "Payment Schedule" tab, `module='Purchase Order'`, `modulecode=pur_order.code`) — **SKIP**: source terms (`trdordpl3payterms`) have no dates + duplicate rows.
- **Actual payments** (`trhpayrg` + `trdpayrg1details`) = the **separate "Payment" module** (timeline row 15 "Receipt Entry"), not the PO build. It links back to a PO via `module`/`modulecode` when built.

---

## 8) Form work (saksham) — simple company-gate additions

Rule: if a field doesn't show for saksham, just add `saksham` to that field's company condition. Needed:
- **Add `saksham` to the existing gate:** Order Status (`txtstatus`), Total Discount (`txtdiscount`).
- **Saksham fields:** Bill To = `cbranchcode` (relabelled, free-hand) + Ship To = `shippingbranch` (free-hand, beside it); Inquiry Category + Pending Reason (`miscellaneous` selects with [+] Quick-Add), Invoice No./Date, O/A No./Date, and a Remarks textarea.
- Task Check List — already there (§5). Order Type / Payment Schedule — skipped.

---

## Build status (done 2026-09-10)

- **00_ALTERS** — added `pur_order` columns: inquirycategory, pendingreason, invoiceno, invoicedate, oano, oadate. Bill/Ship need NO new column (Bill To = existing `cbranchcode`, Ship To = existing `shippingbranch`; branch refs, NOT text). Run on the fresh target BEFORE the exe.
- **exe** — `PurchaseOrder` module added to `Program.cs` (module 6, `RunModule` case 5); header `trhordpl`→`pur_order`, lines `trdordpl1items`→`pur_order_det`; resolves supplier/product/currency, seeds `status` (module='Purchase Order'), Category `miscellaneous(Inquiry/Category)`, Pending Reason `miscellaneous(Purchase Order/Pending Reason)`; Bill To → `cbranchcode`, Ship To → `shippingbranch` (branch refs); salesordercode = comma list of migrated SO `inqcs.code`, collected from ALL PO lines (a blank-item line can carry the only SO link, e.g. OP1606 → OR2128/OR2129). Payment schedule/actual payments NOT migrated. Built Debug (Release deleted). Run order: Contact → Product → CI → CQ → SO → Purchase Order.
- **form/view** (`gst/purchase/default.asp` + `app/purchaseorder.asp` + `script/purchaseorder.js`) — all saksham-gated: added `saksham` to Status & Discount gates; new fields (Inquiry Category + Pending Reason `miscellaneous` selects with [+] quick-add, Invoice No/Date, O/A No/Date, Remarks) shown in **both form and view**; Bill To (`cbranchcode`, relabelled) + Ship To (`shippingbranch`) are free-hand branch pickers ("party - place"). View reads 13 saksham trailing columns appended in `GetPurchaseOrderArray` (JS `length - 13`); save is the generic `SqlOperation` (`txt<col>`→column).

**Tax & Payment (consistency note, 2026-09-12):** a Purchase Order is an *order*, not a tax bill, so **tax is not migrated** (correct). Totals derive from line items. Payment Schedule + actual payments = the separate **Payment** module (§7), not here. This matches the Sales Order (05) and Sales Invoice (07) decision: real line items, no synthetic GST.

_Re-checked 2026-09-15: eBizWiz POs have NO GST charges (0 of 6,031) — nothing to migrate. Line discount: `nrate` is already net of the discount (1,178 of 1,179 discounted lines) → exe no longer stores `ndiscount` in `pur_order_det.discount` (it cut the discount twice). The saksham PO screen (type 'PO') shows no tax / adjustments at all (standard gate in `purchaseorder.js`), so the non-GST PO charges (`trdordpl4posttaxchgs`, 2,180 rows: freight, commission …) are not migrated; `ntotalamount` stays the source reference._

_exe: canonical migration = single-file **`eBizWiztoEdifyBiz\Program.cs`** (.NET 4.7.2)._

_Verified 2026-09-15 (full data): PO → SO link 5,615 POs, 0 wrong; Bill To 4,951 / Ship To 4,933 correct party (1 each: source party no longer exists). OP1606 was missing OR2128/OR2129 (link only on blank-item lines) — exe fixed to read all lines, record patched._

_Line batch (2026-09-15): `pur_order_det.prodbatchcode` = the product's Default Batch (created by the Product module). Verified: 31,362 migrated lines filled, 0 pointing to another product's batch (the only null line is the pre-existing test PO-000123, not migrated)._
