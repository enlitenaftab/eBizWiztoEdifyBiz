# Sales Order — Migration Mapping (eBizWiz → EdifyBiz)

Client module **Order Received** (Sales Order). Read as: what the client sees → where it sits in the client DB → which column it goes to in our DB → how it shows in our UI.

In EdifyBiz a Sales Order is **not** a separate table — it is an `inqcs` row with **`cors='SO'`** (same shared inquiry/quotation form family; the `salesorder\` folder is actually the Sales-Invoice/fulfilment module, not order creation). Line items live in `inqcsdet`. Storage verified in EdifyBiz2026 production (`app\inquiry.asp`, `inquiry\default.asp`, `inquiry\app\Default.aspx.cs`).

Source tables: `trhordrc` (Order Received header), `trdordrc1items` (line items).
Target tables: `inqcs` (cors='SO', header), `inqcsdet` (line items).
Scope: offices 2 (HO Mumbai), 3 (Bangalore), 4 (Delhi), 6 (Kolkata). ~7,658 orders. Test offices 1 & 5 excluded.

> Coverage seen in source: ~94% orders are **against a Quotation** (`nquote`); Bill To / Ship To / Currency / Client Order No almost always present; direct inquiry link (`ninquiry`) is negligible (3 rows).

---

## 1) Order header → `inqcs` (cors='SO')

| Client UI (eBizWiz) | Client DB (`trhordrc`) | Our DB (target column) | Our UI (EdifyBiz) |
|---|---|---|---|
| Trn. No. | `vtrnprefix` + `ntrnno` | `inqcs.inqref` | Order No. (auto) |
| Trn. Date | `dtrndate` | `inqcs.inqdate` | Order Date |
| Order From (FROM PARTY) → the party | `nordrecdfrom` + `nfromparty` → target `contact` (by name) | `inqcs.cscode` | Party / Customer |
| (branch of that customer) | party is per-branch (no field) | `inqcs.csbranch` | Customer Branch (customer's own address) |
| Party Contact 1 + 2 / Order By | `npartycontact` + `npartycontact2` → each `mltcontact.code` | `inqcs.cperson` (both, comma-separated) | Party Contact |
| Order Is (order type) | `nordrecdtype` → `mstfixedselection` (Fresh / Against Quotation - Sales / Service) | `inqcs.ordertype` | Order Type |
| Order Source | `nordsource` → `mstfixedselection` | `inqcs.sources` | Order Source |
| Sales Person | `nsalesman` → target `users` (by name; all source users migrated in Contact) | `inqcs.executive` | Sales Person (real salesperson) |
| Bill To | customer (`nfromparty`) → contact → default `mltaddress` code | `inqcs.csbranch` (int → mltaddress; the existing Customer Branch field = Bill To) | Customer Branch / Bill To (free-hand) |
| Ship To | `nshipto` (party code) → migrated `contact` → its default `mltaddress` code | `inqcs.shippingbranch` (int → mltaddress) | Ship To (free-hand, beside Customer Branch, SO-only) |
| Inquiry Category | `ninquirycategory` → `miscellaneous(Inquiry/Category)` | `inqcs.Category` (SO form reads it as `companycat`) | Inquiry Category |
| Client Order No. | `vrefno` | `inqcs.ponumber` | Client Order No. |
| Client Order Date | `drefdate` | `inqcs.podate` | Client Order Date |
| Order Currency | `ncurrency` → `currency` master (by name / short name) | `inqcs.currency` | Currency |
| (exchange rate) | `nexchangerate` | `inqcs.exchangerate` | Exchange Rate |
| Order Status | `nordrecdstatus` → `status` master | `inqcs.status` | Status |
| Remarks | `vremarks` | `inqcs.remark` | Remarks |

Notes:
- Bill To / Ship To = **branch references** (int → `mltaddress`), decided 2026-09-12 (replaces earlier text `bilingaddress`/`deliveryaddress`, which are removed for saksham). **Bill To = the existing free-hand "Customer Branch" field `inquiry_form_csbranch` → `inqcs.csbranch`** (already migrated = customer's default branch). **Ship To = a new free-hand field `inquiry_form_so_shipbranch` (name `txtshippingbranch`) → `inqcs.shippingbranch`**, placed beside Customer Branch, SO-only (`so_detail_block` class). Both free-hand `select2json` + `data="freebranch"`, label "party - place". Resolve `nshipto` party → contact → default `mltaddress` code.
- `ordertype` / `sources` are the same kind of masters used by the shared form; matched by name (created if missing).

---

## 2) Link back to the source Quotation / Inquiry

| Client UI (eBizWiz) | Client DB (`trhordrc`) | Our DB (target column) | Note |
|---|---|---|---|
| (Against Quotation) | `nquote` → the migrated Quotation | `inqcs.inqlink` (and `parentcode`) | ~94% of orders; resolves to the CQ `inqcs.code` (cors='CQ') |
| (Against Inquiry) | `ninquiry` → the migrated Inquiry | `inqcs.inqcode` | negligible (3 rows) |

Run order: Contact → Product → Customer Inquiry → Customer Quotation → **Sales Order**, so the linked quotation already exists when the order is migrated.

---

## 3) Line items → `inqcsdet`

| Client UI | Client DB (`trdordrc1items`) | Our DB (`inqcsdet`) | Our UI |
|---|---|---|---|
| Product | `nitem` → target `product` (by name) | `pcode` | product row |
| Quantity | `nquantity` | `quantity` | quantity |
| Rate | `nrate` | `price` | price |
| Discount % | `ndiscountperc` | `discountpercent` | discount % |
| Line Currency | `nitemcurrency` → `currency` master | `currency` | currency |

---

## 4) Fields folded into Remarks (no structured target)

Appended (labelled) into `inqcs.remark`, nothing lost:

| Client UI (eBizWiz) | Client DB (`trhordrc`) |
|---|---|
| Comment | `vcomment` |
| Pending Reason | `npendingreason` → `mstfixedselection` |
| Prn. Order Ackn. No. | `vordacknno` |
| Prn. Order Ackn. Date | `dackndate` |

---

## 5) Order Check List → `taskchecklist` (standard module-based task checklist)

Per senior: the check list is **not** a header field — it uses our **existing** module-based **task checklist** (the same `taskchecklist` component used by the Task and Daily Work modules). It is just **enabled** for the Sales Order form (saksham, `showso`); nothing custom is built. **No task record is created** — checklist items attach directly to the order.

Keying (same convention as Daily Work): `taskchecklist.module = 'SO'`, `taskchecklist.modulecode = Sales Order inqcs.code`. Distinct 'SO' tag (NOT 'TSK', which is the real Task module) so no collision. The Check List tab reads `taskchecklist where module='SO' and modulecode=<SO code>`.

Source: `trdordrc5checks` (per-order check items) + `mstchecks` (check name master). Keyed per office (`nofficeid|nordrc`). ~149,662 items across ~4,702 orders (in scope).

| Client UI (eBizWiz) | Client DB (`trdordrc5checks`) | Our DB (`taskchecklist`) | Our UI |
|---|---|---|---|
| (check item name) | `ncheck` → `mstchecks.vname` | `title` | Title |
| Done? | `bdone` | `status` (1=Done / 0=Pending) | Status (checkbox) |
| Expected Date | `dexpdate` | `duedate` (if none: open→+7 days, done→done/created date) | Due Date |
| Alloted To | `nallotedto` → target `users` (by name) | `assignto` | Assign To |
| Remarks | `vremarks` | `remark` | Remark |
| Done Date | `ddonedate` | `updatedon` (when done) | Completed Date |
| (added by / on) | `addedby` / `addedon` | `createdby` / `createdon` | — |
| — | (fixed) | `module='SO'`, `modulecode`=SO `inqcs.code` | (ties item to the order) |

---

## 6) Not migrated / auto (no target, or filled by the system)

| Client UI | Client DB | Reason |
|---|---|---|
| Terms & Cond. | `nterms` / `vtermsconditions` | the exe writes `inqcs.terms` as NULL (not carried); source is sparse |
| Select Letterhead to print | `vletterhead` | chosen at print time, not stored data |
| Trn. Total | `ntotalamount` etc. | derived from the line items |
| Order From = FROM OFFICE / FROM USER | `nfromoffice` / `nfromuser` | rare; base migration treats the order as customer (FROM PARTY) |

---

**Tax & Payment (2026-09-12, tax part superseded 2026-09-15 — see "Tax, charges & discount" below):** ~~a Sales Order is an *order*, not a tax invoice, so tax is not migrated~~. Source line tax is empty anyway (see 07 §3). Totals are derived from the line items. Actual payments/receipts belong to the separate **Payment** module (timeline row 15), not here. This matches the Purchase Order (06) and Sales Invoice (07) decision: real line items, no synthetic GST.

## Tax, charges & discount (corrected 2026-09-15 — replaces "tax not migrated")

eBizWiz SOs carry GST as **post-tax charges** (`trdordrc4posttaxchgs` + `mstprepostchgs`): 3,186 of 7,658 SOs, same structure and formula as the Sales Invoice (07 §5). Formula check vs `ntotalamount`: 3,178 of 3,186 GST SOs, 7,641 of 7,658 overall. 113 SOs also use the line Tax Set (`ntaxset`/`ntaxamt`).

How the saksham SO view works (`assets/scripts/inquiry.js` `handleProductViewFill`, generic path): line = qty × (price − `discount`) (+fca/others); **no tax column and tax is never added**; `inq_adjust` rows are listed under Adjustments and added → Grand Total. `inqcsdet.prodtax` = the line "Tax %" field.

| eBizWiz | Rule | Our DB |
|---|---|---|
| Line rate `nmasterrate`, `ndiscountperc`, `nrate` (net) | `nrate` = master × (1 − %) on all 20,702 discounted lines. Discounted line → price = `nmasterrate`, discount = master − `nrate` (per unit), discount % kept; else price = `nrate` | `inqcsdet.price`, `discount`, `discountpercent` |
| GST % | main GST charge of the SO (IGST x / CGST x + SGST y / GST x; largest of a kind); none → line Tax Set % | `inqcsdet.prodtax` (all lines of the SO) |
| Every charge, GST included ("Add : IGST @18%", freight, discount, round off, TOTAL rows with amount …) | amount, or item total × % (07 §5 formula), in entry order, zero skipped | `inq_adjust` (adjustname, adjustpercent, adjustamount) |
| Line Tax Set amounts | sum of `ntaxamt` | `inq_adjust` "Tax on items (eBizWiz)" |
| Unmapped product lines | qty × `nrate` | `inq_adjust` "Other items (eBizWiz)" |

Result: SO Grand Total in EdifyBiz = eBizWiz total; the GST lines read exactly as the client printed them. `inqcsdet.cgst/sgst` are numeric(18,0) (can't hold 2.5) and unused by this view → not written. `inq_adjust.adjustname` is varchar(max) → no ALTER. Note: EdifyBiz never copies `inq_adjust` on CQ→SO conversion (standard behaviour).

_exe: the canonical migration is the single-file **`eBizWiztoEdifyBiz\Program.cs`** (.NET 4.7.2), `SalesOrder` class (`inqcs` cors='SO' family; also fills `taskchecklist` for the Check List tab). No ALTER needed — `inqcs` and `taskchecklist` target columns above already exist. Run order: Contact → Product → Customer Inquiry → Customer Quotation → Sales Order._

_UI (saksham): Sales Order form gets the standard **Check List** tab (`#tabTaskchecklist`, `showso`), same component as Task/DailyWork — markup in `inquiry\default.asp`, generic `/edify/task/` backend (`taskchecklist`, `module='SO'`) via the reusable `TaskCheckListFill` component wired in `assets\scripts\inquiry.js`._

_Verified 2026-09-15 (full data): SO → Quotation link 7,214 of 7,214 and SO → Inquiry 3 of 3, 0 wrong; Ship To 7,456 of 7,458 correct party (2 source parties no longer exist); check list 149,662 rows, module='SO', all pointing to SO rows._

_Verified 2026-09-15 (run before the % fix): all 7,658 SOs — line value qty × (price − discount) = eBizWiz qty × `nrate` (0 wrong); Tax % = the GST label/percent in 3,159 of 3,166 (the 7: several GST rates on one SO — exe takes the largest-amount one, or the entered % differs from the label, e.g. OR887 "IGST @18%" entered 5%); 100%-discount lines (free items, net 0) are real client data. Total vs eBizWiz 7,641 of 7,658 — remaining ones fixed by the "% on item total" correction or are source totals that don't add up._

_Run 2 verified 2026-09-15 (after "% on item total"): total = eBizWiz on all SOs with a consistent source total except 2 (1 source header ≠ its lines, 1 other). Tax % reads "NIL"/>28 % labels as 0 and a GST label with no amount as no GST (exe fix same day)._
