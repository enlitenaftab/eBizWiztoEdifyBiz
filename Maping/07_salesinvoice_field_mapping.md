# Sales Invoice — Migration Mapping (eBizWiz → EdifyBiz)

Client module **Warranty / Non-Warranty Sales** ("Product Sale Entry"). Read as: what the client sees → where it sits in the client DB → which column it goes to in our DB.

EdifyBiz target = the **GST Sales** module, `sal_order` (`type='SO'`), line items = `sal_order_det` (`salcode` → `sal_order.code`), serial/warranty on the same line row. Live screen = `gst/sales/default.asp`. Save = generic `SqlOperation("add"/"edit","sal_order")` (`txt<col>` → column).

> This is the GST invoice (`trhsales`), NOT the CRM `inqcs` pipeline. The CRM Sales Order (Order Received) already went to `inqcs cors='SO'` (module 05).

Source tables: `trhsales` (header), `trdsales1items` (lines), `trdsales2itemsdet` (serial/warranty), `trdsales6checks` (check list), `trdsales4posttaxchgs` + `mstprepostchgs` (GST + charges). Scope: offices 2/3/4/6 — ~5,707 invoices, ~18,830 lines.

---

## 1) Header → `sal_order` (`type='SO'`)

| Client UI (eBizWiz) | Client DB (`trhsales`) | Our DB (`sal_order`) |
|---|---|---|
| Trn. No. | `vtrnprefix` + `ntrnno` | `salorderno` |
| Trn. Date | `dtrndate` | `salorderdt` |
| Party | `nparty` → target `contact` (by name) | `ccode` |
| Party Contact | `npartycontact` → `mltcontact.code` | `contactperson` |
| Sales Person | `nsalesman` → target `users` (by name) | `executive` (fallback Admin) |
| Bill To | `nbillto` (party) → contact → default `mltaddress` code; **blank → the party's own branch** (`nparty`) | `cbranchcode` (free-hand branch) |
| Ship To | `nshipto` (party) → contact → default `mltaddress` code | `shippingbranch` (free-hand branch) |
| Sales Type | `nsalestype` → `mstfixedselection` (FRESH / AGAINST QUOTATION / AGAINST ORDER RECEIVED / AGAINST UNBILLED CHALLANS) | `ordertype` (name). Dropdown master = `miscellaneous` (module `Sales`, type `Sales Type`), full group seeded by the exe, form [+] quick-add |
| Against Quotation / Order | `nquote` → migrated CQ, `nordrc` → migrated SO | `inqcsso` (linked `inqcs.code`) |
| P.O. No. | `vpono` | `ponumber` |
| P.O. Date | `dpodate` | `purorderdt` |
| Dispatch Mode | `ndispatchmode` → `mstfixedselection` | `dispatchmode` |
| Dispatch Through | `vdespatchthru` | `couriername` |
| Docket No. | `vdespdocno` | `courierno` |
| Docket Date | `ddespdocdate` | `dispatchdate` |
| Remarks + Comment | `vremarks` + `vcomment` | `remarks` |
| Ref. No. / Ref. Date | `vrefno` / `drefdate` | `remarks` (folded) |
| (office) | `nofficeid` | `branchcode` |
| — | — | `comcode` = company, `type` = 'SO', `currency` = INR |

Notes:
- **Bill To / Ship To = branch reference** (int → `mltaddress`), free-hand pickers (`select2json` + `data="freebranch"`), same as SO/PO. No text, no new column.
- **Bill To blank** in eBizWiz = billed to the party itself → the exe uses the party's branch (needed for the customer GST state, §5).
- **Ref No/Date + eBizWiz invoice total + amount received** are folded (labelled) into `remarks` for reference. Freight/charges are no longer in remarks — they are real adjustments (§5).

---

## 2) Line items → `sal_order_det`

| Client UI | Client DB (`trdsales1items`) | Our DB (`sal_order_det`) |
|---|---|---|
| Product | `nitem` → target `product` (by name) | `prodcode` |
| Quantity | `nquantity` | `qty` |
| Rate | `nrate` | `price` |
| Discount | `ndiscount` | **not migrated** (already inside `nrate`); `discount` holds only a pre-GST invoice discount spread by the exe (§5) |
| GST rate | client GST charge / line Tax Set (§5) | `gst` (CGST+SGST or IGST %) |
| — | — | `currency` = INR |

FK = `sal_order_det.salcode = sal_order.code`.

- **`nrate` is already the net rate.** eBizWiz item total = qty × `nrate` (header `nitemtotal` matches in 5,657 of 5,673); `ndiscount` is never deducted (1,930 lines, mostly junk like 1.00). Migrating it as `discount` cut the discount twice → dropped.
- Blank-item lines (no product, 63 lines with a rate) can't be `sal_order_det` rows; their value goes to the "Other items (eBizWiz)" adjustment (§5) so the total still matches.

---

## 3) Serial / Warranty → `sal_order_det` (same line row)

Per-serial detail (`trdsales2itemsdet`, linked to the line via `nsales1` = `trdsales1items.ncode`) is aggregated onto the line:

| Client UI | Client DB (`trdsales2itemsdet`) | Our DB (`sal_order_det`) |
|---|---|---|
| Serial No. | `vserialno` (all serials of the line, comma-joined) | `installedremarks` ("Serial No: …") |
| Warranty Months | `nmonths` | `warrantymonths` |
| Installation Date | `dinstdate` | `installationdt` |
| Location | `vlocation` | `location` |

Serial numbers are stored as **text** in `installedremarks` (not the `prodbatchsrnocode` serial master) — lossless, no master build. `installedremarks` is widened nvarchar(500) → nvarchar(1000) in `00_ALTERS` (a few lines carry up to 60 serials, ~730 chars). PM visits / OEM warranty / start-end dates not carried.

---

## 4) Check List → `taskchecklist`

`trdsales6checks` (+ `mstchecks`) → `taskchecklist` (`module='SAL'`, `modulecode = sal_order.code`; distinct tag, NOT 'TSK'), same component as the Sales Order (`SAL` is also the tag the sales module already uses for its Followup / Task List tabs). The standard **Task Check List** tab is added to the sales **view** panel (saksham) and filled on view. 56 items migrated (mainly for new entries going forward).

---

## 4b) Warranty / Non Warranty → Sales label (added 2026-09-15)

eBizWiz has two bill menus — **Warranty Sales** (machines, with warranty) and **Non Warranty Sales** ("Spare Part Sale Entry") — both saved in `trhsales`, told apart only by `bwarranty` (1 = Warranty 4,313 · 0 = Non Warranty 1,394). EdifyBiz Sales Invoice has no such field, so the exe tags it with the **standard sales Labels** (no form change):

| Client | Client DB | Our DB |
|---|---|---|
| Warranty Sales / Non Warranty Sales | `trhsales.bwarranty` | `labelrelation` (`label` = label code, `module` = `sal_order.code`, createdon = invoice created, createdby = Admin) |
| (label master) | — | `label` rows `category='SAL'`: "Warranty Sales" (#29BF94), "Non Warranty Sales" (#FBC617) — reused by name if present, else created |

How EdifyBiz uses it (checked in code): the sales list Labels button reads `label where category='SAL'` (`gst/sales/default.asp`), `action=label` saves `labelrelation(label, module=sal_order.code)` (`gst/sales/app/sales.asp` → `LabelRelation` class in `app/include.asp`), the list shows them via `labelname1(code,'SAL')`, and label search filters `a.code in (select module from labelrelation where label in …)`.

---

## 5) GST + charges → `sal_tax` / `sal_adjust` (migrated, decided 2026-09-15)

The earlier "tax not migrated" decision was wrong: the client's real GST (₹15.7 Cr on 2,069 invoices) sits in the source and without it invoice totals, GST reports and the Payment module (receipts include GST) would not match.

### How eBizWiz stores GST
- **Not per product, no HSN.** The line "Tax Set" was used on only 29 lines. GST was added **below the invoice as a post-tax charge** (`trdsales4posttaxchgs`, name from `mstprepostchgs`): "Add : IGST @18%", "Add : CGST @9%" + "Add : SGST @9%", "Add : GST @18%", GST 5/12/28 %, concessional, SEZ Nil …
- A charge is entered as **AMOUNT** (`namount`) or **PERCENTAGE** (`ntaxpercentage`, amount NOT stored).
- **Formula (corrected 2026-09-15 after record checks):** a **PERCENTAGE charge (discount or GST) is always item total × %** (item total = qty × `nrate`, all lines); AMOUNT charges as entered. Total = items + line tax + all charges. Label rows ("TOTAL …") are charges too (amount if any). Verified on documents that have % charges vs `ntotalamount`: invoices 1,603 of 1,604, SO 3,373 of 3,374, quotations 22,223 of 22,236 (the first rule, "% on items + earlier charges", matched fewer: 1,599 / 3,358 / 22,095 — e.g. QT29172: −20% discount and CGST 9% + SGST 9% all on the item total).
- **All saksham offices billed on the Maharashtra GSTIN:** Maharashtra customer → CGST+SGST (512 of 512), other state → IGST (1,031 of 1,046); Bangalore/Delhi/Kolkata offices charged IGST to their own-state customers.

### How EdifyBiz shows GST (sales view)
- Tax per line = line value × `sal_tax.taxpercent`, joined **by product + tax (CGST/SGST/IGST)** — one row per product per tax.
- CGST+SGST vs IGST is decided by the view: branch `companyaddress` city → state → `stategstcode` **=** Bill To `mltaddress` city → state → `stategstcode`.
- `sal_adjust` (beforetax 0) rows are shown under "Adjustments" and added after tax → grand total.

### Mapping (exe)
| What | Rule | Our DB |
|---|---|---|
| State GST codes | official code by state name (misspellings too: Telngana 36, Uttaranchal 05, CHENNAI 33, NOIDA 09); only where empty; foreign states stay empty (= IGST, export) | `state.stategstcode` |
| Branch GST state | office 2 (HO Mumbai) city → all 4 branches whose `citycode` is NULL | `companyaddress.citycode` (+ `statecode`) |
| Product GST rate | total % of the main GST charge (IGST x / CGST x + SGST y / GST x; several of one kind → the largest amount; "NIL" labels and > 28 % = 0 — e.g. "GST : NIL for 100% EOU"; GST label with no amount charged = no GST). No header GST → line Tax Set % | `sal_order_det.gst` |
| GST type | **follows the view's state rule** (not the label): branch state 27 = customer Bill To state → CGST %/2 + SGST %/2, else IGST % — so every invoice shows its GST (e.g. SA1683 CGST+SGST label, Gujarat customer → IGST 18) | `sal_tax` (per product, `taxpercent`, `taxamount` = line value × %, `taxstatefrom` = Maharashtra, `taxstateto` = customer state or 0) |
| Pre-GST discount | GST entered as AMOUNT and equal to % × (items − earlier AMOUNT discount [+ freight]) → that discount was before GST: spread over the lines by value (last line takes the rounding) instead of an adjustment (e.g. SA2071: −56,71,160 discount, GST 18 % on 61,00,000) | `sal_order_det.discount` (line amount) |
| Non-GST charges (freight, packing, insurance, discount, round off, cess, TOTAL rows with amount …) | amount (or item total × %) in entry order, zero skipped | `sal_adjust` (name, % , amount) |
| GST on charges ("GST @18% on Customs Clearance …") | as its own charge | `sal_adjust` |
| GST the lines can't carry | eBizWiz GST − what the view computes (per line, rounded like sales.js): freight/charges were taxed → "GST on charges (eBizWiz)"; up to ₹1 → "GST rounding (eBizWiz)"; else (client charged GST on only part of the items, mixed rates, blank-item lines) → "GST difference (eBizWiz)" | `sal_adjust` |
| Blank-item / unmapped lines | their qty × rate | `sal_adjust` "Other items (eBizWiz)" |

Result: EdifyBiz grand total = eBizWiz formula total for every invoice. `adjustname` widened varchar(20) → varchar(100) in `00_ALTERS` (charge names up to 100 chars).

Limits (known, not hidden): simulated on full data for the 2,048 invoices with a GST rate — line tax exact 1,347, rounding ≤ ₹1 293, GST on charges 167, ₹1–100 12, > ₹100 ~229 (client charged GST on only part of the invoice / mixed rates / unmapped lines — these keep a "GST difference (eBizWiz)" line; total still right). Where the label disagrees with the state rule (~55) the tax type shown follows the state rule. 5 GST invoices whose own `ntotalamount` doesn't follow the formula keep the formula total. **Client to confirm:** Bangalore/Delhi/Kolkata have no separate GSTIN (data says all billed on Maharashtra). If they do, change those branch cities in Company Address — only the display of CGST/SGST vs IGST changes.

- **Payment — separate Payment module** (Receipt Entry). Payment schedule (`trdsales3payterms`) + amount received (`namountrecd`) migrate there, linked to `sal_order`; not in this module.

---

## 6) Not migrated / folded

| Client UI | Client DB | Reason |
|---|---|---|
| Sales Source | `nsalessource` | 98% NULL / garbage |
| Campaign | `ntrhcampa` | 0 rows |
| Terms & Cond. | `nterms` / `vtermsconditions` | source blank |
| Line discount | `ndiscount` | already inside `nrate` (§2) |
| Demo / Letterhead / Trn. Total | `bdemo` / `vletterhead` / totals | flag / print / derived |

---

## Build status

Built 2026-09-12 — `SalesInvoice` class in `Program.cs` (module 7). **`00_ALTERS` entries:** widen `sal_order_det.installedremarks` to nvarchar(1000) (serial numbers) and `sal_adjust.adjustname` to varchar(100) (charge names, added 2026-09-15). Everything else reuses existing columns (bill/ship → `cbranchcode`/`shippingbranch`; ref/total → `remarks`; warranty → `sal_order_det`; GST → `sal_tax` + `sal_order_det.gst`; charges → `sal_adjust`). GST setup (state codes, branch GST city) = `Shared.EnsureGstSetup`, run at the start of the module. Form (saksham): Bill To / Ship To free-hand (edit + view label "party - place" from `app/sales.asp`; customer-change branch refill skipped for saksham), Sales Type field in form + view, Task Check List tab in the view panel (`gst/sales/default.asp` + `script/sales.js` + `app/sales.asp`). Run order: Contact → Product → CI → CQ → SO → Purchase Order → Sales Invoice.

_exe: canonical migration = single-file `eBizWiztoEdifyBiz\Program.cs` (.NET 4.7.2)._

## Verified (2026-09-15, full data, source vs target)

- Invoices 5,707 = 5,707 (0 errors). Lines 18,693 of 18,694 — the 1 skipped line is item "testing 123" from test office 1 (out of scope).
- Header fields 0 mismatch: date, customer, executive, Sales Type, P.O. No/Date, dispatch mode/through, docket no/date, branch, currency, Ref No, Amount Received, Invoice Total.
- Bill To 4,284 / Ship To 4,675 — all point to the correct party (incl. cases where Bill/Ship party ≠ customer).
- Contact person 3,150 of 3,156 — the 6 have a source `npartycontact` that does not belong to that party (broken source ref), left blank.
- Lines per invoice (count, qty, rate, discount, product set), serials 6,936 of 6,937, warranty months, install date, location, check list 56 = 56 (title, done, due date) — 0 mismatch except the 1 test-office line.
- Links: Invoice → SO/CQ 3,327 of 3,327 linked, 0 wrong.

_Line batch (2026-09-15): `sal_order_det.prodbatchcode` = the product's Default Batch (created by the Product module) — Batch No is required on invoice lines and the invoice product search only lists products with a batch. Verified: 18,693 / 18,693 lines filled, 0 pointing to another product's batch._

_Run 2026-09-15 (ALTER for `adjustname` missing → 1,756 invoices with charges failed and rolled back; re-run needed): the 3,951 that migrated check out — `sal_tax` 0 duplicates, `sal_order_det.gst` = `sal_tax` % on 7,777 of 7,777 lines, no discount, branches 3–6 on GST state 27, 44 state codes filled; view-style total = eBizWiz total on 2,851 of 3,040 with a source total, the 189 others are 2017 invoices whose source total doesn't add up (e.g. SA90 items 877,533, no charges, total 2,510)._

_Run 2 verified 2026-09-15 (5,707 invoices, 0 errors): `sal_tax` 0 duplicates; `sal_order_det.gst` = `sal_tax` % on 18,693 of 18,693 lines; view-style total = eBizWiz total on 4,606 of 4,796 with a source total (190 = 2017 invoices whose source total doesn't add up, e.g. SA90). 832 "GST difference" lines found → causes fixed in exe: NIL/100% EOU label read as 100 %, GST type vs state rule, pre-GST amount discounts, GST label without amount. SA2486 checked line by line: 2 × 3,81,356, CGST 9 % 68,644 + SGST 9 % 68,644 = 9,00,000 (0.16 rounding)._

_Run 3 verified 2026-09-15 (final tax rules, 5,707 invoices, 0 errors): total = eBizWiz on 4,606 of 4,796 with a source total (190 = 2017 source totals that don't add up); 2,063 taxed, tax type vs state conflicts 0; `sal_tax` duplicates 0; `gst` = `sal_tax` % 18,693/18,693; pre-GST discount on 666 lines / 302 invoices (0 negative / above line value). Adjustments: client charges 3,840, GST on charges 167, GST rounding 284 (−₹15 total), Other items 62, GST difference 238 (client charged GST on only part of the invoice, e.g. SA1747 ₹3,375 GST on ₹42.7 L items). Record checks: SA2071 61,00,000 + CGST 9 % + SGST 9 % = 71,98,000 (no extra line); SA1818 EOU NIL = 10,00,000 no tax; SA1683 Gujarat → IGST 18 % 35,84,737 + round-off = 2,35,00,000; SA1792 no GST 7,86,000 − 3,05,800 = 4,80,200; SA2486 9,00,000._
