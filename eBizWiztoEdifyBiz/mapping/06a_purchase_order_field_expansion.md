# 06a — Purchase Order : Field Expansion Plan

Planning analysis for the new PO work (meeting 2026-11-18). The client wants the Order Placed
advanced header fields — today folded into `pur_order.remarks` (see [06_purchase_order.md](06_purchase_order.md) §2) —
to become **dedicated fields** on the PO form, plus role-based visibility for the four Commission fields.

**Scope of this doc:** only the DB/migration mapping — which source column lands where, what is already
there, what must be added. The UI (gst/purchase form + purchaseorder.js) and the role gate are separate build items.
**Nothing is implemented yet.** Verified against source `saksham70v1_1` and target `SakshamRMtP15329`.

Source header: `trhordpl` · Target header: `pur_order` (`type='PO'`). Fill counts are of 6,293 POs.

---

## A. Already mapped to dedicated columns (no change)

From [06_purchase_order.md](06_purchase_order.md) §1.1 — these form fields are done:

| Form field | `trhordpl` | `pur_order` |
|---|---|---|
| Trn. No. | `vtrnprefix`+`ntrnno` | `purorderno` |
| Trn. Date | `dtrndate` | `purorderdt` |
| Order To | `ntoparty` | `scode` |
| Pending Reason | `npendingreason` | `pendingreason` |
| Terms & Cond. (set) + Payment Terms grid | `nterms` / `trdordpl3payterms` | `term` |
| Order Currency | `ncurrency` | `currency` |
| Inquiry Category | `ninquirycategory` | `inquirycategory` |
| Bill To | `nbillto` | `cbranchcode` |
| Ship To | `nshipto` | `shippingbranch` |
| Invoice No. | `vinvno` | `invoiceno` |
| Invoice Date | `dinvdate` | `invoicedate` |
| O/A No | `vrefno` | `oano` |
| O/A Date | `drefdate` | `oadate` |
| Total Discount % | `nprindiscountperc` | `discount` |
| Order Status | `nordplcdstatus` | `status` |
| Check List (items) | `trdordpl5checks` | `taskchecklist` (module='PUR') |

---

## B. Reuse an existing column (no new column)

None. (Order Type was a candidate to reuse `ordertype`, but the user dropped it — see §D.)

---

## C. New columns required on `pur_order`

Today these are written into `pur_order.remarks` text ([06](06_purchase_order.md) §2). Client wants each as its own field.

| # | Form field | `trhordpl` source (type) | New `pur_order` column (proposed) | Filled |
|---|---|---|---|---|
| 1 | Commission Requested 🔒 | `nprincommreq` decimal(13,9) | `commissionrequested` numeric(18,2) | 766 |
| 2 | Commission Requested Currency 🔒 | `nprinreqcurrency` int → `currency` | `commissionreqcurrency` int | 766 |
| 3 | Commission Received 🔒 | `nprincommrec` decimal(13,9) | `commissionreceived` numeric(18,2) | 253 |
| 4 | Commission Received Currency 🔒 | `nprinreccurrency` int → `currency` | `commissionreccurrency` int | 253 |
| 5 | OR Value Payment Received % | `nprinorpayrecdperc` decimal(5,2) | `orvalpayrecdperc` numeric(13,2) | 355 |
| 6 | OR Value Payment Received Amt | `nprinorpayrecdamt` int | `orvalpayrecdamt` numeric(18,2) | 355 |
| 7 | OR Value Payment Pending % | `nprinorpaypenperc` decimal(13,2) | `orvalpaypenperc` numeric(13,2) | — |
| 8 | OR Value Payment Pending Amt | `nprinorpaypenamc` decimal(13,2) | `orvalpaypenamt` numeric(18,2) | 169 |
| 9 | Payment Pending Since Date | `dprinpaypenddate` datetime | `paymentpendingdate` datetime | 66 |
| 10 | Total INR Value of Custom Clearance | `nprininrcustcleaamt` decimal(18,0) | `custclearinrvalue` numeric(18,2) | 1 |
| 11 | Custom Clearance Recd. Amt. | `ncustomclerecamt` decimal(18,0) | `custclearrecdamt` numeric(18,2) | — |
| 12 | Custom Clearance Pending Amt. | `ncustomclependamt` decimal(18,0) | `custclearpendamt` numeric(18,2) | — |
| 13 | CCS Remarks | `vccsremarks` varchar(max) | `ccsremarks` nvarchar(max) | 7 |
| 14 | Dispatch Date | `dprindispatchdate` datetime | `dispatchdate` datetime (separate from `deliverydate`) | 250 |
| 15 | Installation Date | `dprininstdate` datetime | `installationdate` datetime | 34 |
| 16 | Comment | `trhordpl.vcomment` nvarchar(max) — a comment **log** | `comment` nvarchar(max) | 25 |

🔒 = role-based: visible/editable only to the OPD / full-access role (meeting decision: role gate, not password).

**Comment (#16) — how it migrates:** `vcomment` is not a single comment; it is a running log, one entry per `///`,
each entry `dd/MM/yyyy HH:mm:ss*User=text`. The eBizWiz "Comments Details" popup (eye icon) dumps it raw, one line
per entry, empty save-stamps included. Only 25 of 6,293 POs have any, and most entries are **empty save-stamps**
(no text after `=`) with usually one real note (e.g. OP1233 → `Purchase Order No. 4500001240`, OP817 → `Free Replacement`).
Migration keeps **only the entries with real text**, formatted `dd/MM/yyyy — User: text` (newest first), one per line;
empty stamps are dropped. Decided with the user from the OP1233 visual (2026-11-18). This replaces the raw blob that
`BuildRemark` folds into `pur_order.remarks` today.

**Decided (user, 2026-11-18):**
- **Order Type** (`nordplcdtype`: ON PARTY / ON OFFICE) → **skip**. EdifyBiz PO is always on a supplier (`scode`); ON OFFICE is 10 rows. See §D.
- **Order Placed Type** (`nordertype`: FRESH / AGAINST ORDER RECEIVED) → **skip**. Already carried by `salesordercode`: SO-link present ⇒ Against Order Received, absent ⇒ Fresh. Proof: AGAINST 5717 → 5658 (99%) have the link; FRESH 394 → 387 (98%) have none. If a form label is wanted, derive it from `salesordercode`, no column. See §D.

---

## D. No new column — computed / skip

| Form field | `trhordpl` | Reason |
|---|---|---|
| **Order Type** (ON PARTY / ON OFFICE) | `nordplcdtype` | User-dropped. EdifyBiz PO is always on a supplier (`scode`); ON OFFICE = 10 rows |
| **Order Placed Type** (FRESH / AGAINST ORDER RECEIVED) | `nordertype` | Carried by `salesordercode`: link present ⇒ Against Order Received, absent ⇒ Fresh (99% / 98% match). Convert-from-SO vs Add-fresh sets it on the form |
| Payment Pending Days | `nprincpendingdays` | Read-only; computed in UI = today − Payment Pending Since Date |
| Trn. Total | `ntotalamount` (+`nitemtotal`/`ntotaldiscount`/`ntotaltaxamt`) | Recomputed from the lines |
| Show Items that have reached MOL or ROL | `bordmslandrol` | Screen filter for item picking, not data |
| Show Parts Required of Service Calls | `bpendingpart` | Screen filter, not data |
| Select Letterhead to print | `vletterhead` / `vletterheadlink` | eBizWiz report print option |

---

## E. Count summary

| Bucket | Count |
|---|---|
| Already mapped (A) | 16 fields |
| Reuse existing column (B) | 0 |
| **New columns, confirmed (C #1–16, incl. Comment)** | **16** |
| Computed / skip (D) incl. Order Type + Order Placed Type | 7 |
| **Total new columns to add** | **16** |

All new columns are nullable; the migration moves the value out of `remarks` into its own column and the
`remarks`-folding in `BuildRemark` is removed for those rows. `ordertype` reuse and every new column affect only
the saksham target DB (saksham-gated form); `pur_order` is shared, so the ALTERs script to the saksham DB only.

## F. Build items that follow this mapping (not in this doc)
1. `00_db_alters.md` — ALTER TABLE `pur_order` ADD the 15 (±2) columns.
2. `Program.cs` `PurchaseOrder` — write each source column into its new column; drop it from `BuildRemark`.
3. UI — add the fields to the saksham PO form + `purchaseorder.js`.
4. Role gate — Commission ×4 visible only to OPD/full-access role.
5. Contact Person 1 & 2 (separate meeting point) + print.
