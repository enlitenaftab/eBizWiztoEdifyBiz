# 05. Order Received → Sales Order

Format: see `00_MAPPING_FORMAT.md`.

## 1. Overview

| | Client (eBizWiz) | Ours (EdifyBiz) |
|---|---|---|
| Screen / menu | **Order Received** — header, *Order Received Items*, *Post Tax Charges*, *Payment Terms*, *Terms & Cond.*, *Check List*, *Upload Doc*, *Expenses* (the **More** menu) | **Inquiry** module, document type `SO` (Sales Order) — form (*Sales Order Details* section), view, Product Details (lines + Adjustments + Term Details), Task Check List tab |
| Tables | `trhordrc` (header), `trdordrc1items` (items), `trdordrc2despdate` (delivery date per line), `trdordrc3payterms` (payment terms), `trdordrc4posttaxchgs` (charges), `trdordrc5checks` (check list), `trdordrc6expenses` (expenses); masters `mstfixedselection` (Order From / Order Is / Order Source / Order Status / expense type), `mstcallpendingreasons` (Pending Reason), `mstinquirycategory`, `mstcurrency`, `mstpaymentterms`, `mstcheckset` + `msdcheckset`, `mstchecks`, `mstprepostchgs`, `msttaxset` | `inqcs` (`cors = 'SO'`), `inqcsdet`, `inq_adjust`, `taskchecklist` (`module = 'SO'`); masters `status` (Sales Order), `miscellaneous` (Inquiry / Order Type, Category), `currency`, Checklist Master (`taskchecklistmaster` / `taskchecklistmasterdet`) |
| Code | — | form + view `inquiry/default.asp`, JS `assets/scripts/inquiry.js`, backend `app/inquiry.asp` (Order Type by field name; Source + Category through the saksham SO block), check list `assets/scripts/taskchecklistfill.js` |
| Exe | — | module **5. Sales Order** — class `SalesOrder` in `Program.cs` (after module 4, so the linked quotation exists) |
| Scope | offices 2, 3, 4, 6 (1 and 5 are WinMax test offices) — 7,658 orders | |
| Status | | migrated and verified |

---

## 2. Field mapping

### 2.1 Header → `inqcs` (`cors = 'SO'`)

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 1 | Trn. No. | `trhordrc.vtrnprefix` + `ntrnno` | `inqcs.inqref` | Reference No | Reference Number | e.g. `OR3623` |
| 2 | Trn. Date | `dtrndate` | `inqcs.inqdate` | Sales Order Date | Sales Order Date | |
| 3 | Order By (the ordering party) | `nfromparty` → `mstparty` | `inqcs.cscode` | Customer Name | Customer Name | party → contact (module 1) |
| 4 | Order From | `nordrecdfrom` → `mstfixedselection` | — | — | — | screen selector (FROM PARTY on 7,655 of 7,658); the party itself is row 3 |
| 5 | Bill To | `nbillto` → `mstparty` → its contact's address | `inqcs.csbranch` | **Customer Billing Branch** ✎ | **Customer Billing Branch** ✚ | free-hand branch picker, so any party's branch is valid (the client's own Bill To party on 670 orders); blank → the ordering party's own branch; view shows party - place - address |
| 6 | Ship To | `nshipto` → `mstparty` → its contact's address | `inqcs.shippingbranch` | **Customer Shipping Branch** ✚ | **Customer Shipping Branch** ✚ | same free-hand picker (SO only); view shows party - place - address |
| 7 | Party Contact 1 + Party Contact 2 | `npartycontact`, `npartycontact2` → `msdparty` | `inqcs.cperson` | Contact Person | Contact Person | both, comma-separated `mltcontact` codes |
| 8 | Order Is | `nordrecdtype` → `mstfixedselection` | `inqcs.ordertype` | **Order Type** ✚ | **Order Type** ✚ | name as text; full client group seeded |
| 9 | Order Source | `nordsource` → `mstfixedselection` | `inqcs.sources` | **Source** ✚ | **Source** ✚ | name as text |
| 10 | Sales Person | `nsalesman` → `mstusers` | `inqcs.executive` | Executive | Executive Name | user by name; Admin when not found |
| 11 | Pending Reason | `npendingreason` → `mstcallpendingreasons` ("OPD - …", same master as the PO and the Complaint) | part of `inqcs.remark` | Remarks | Remarks tab | `Pending Reason: …` on all 3,001 orders. **Fixed 23/09/2026 (verified in the DB):** it was read from `mstfixedselection` before, which gave unrelated words (WARM, YES, OPEN) on 2,890 orders. |
| 12 | Inquiry Category | `ninquirycategory` → `mstinquirycategory` | `inqcs.Category` | **Category** ✚ | **Category** ✚ | `miscellaneous` Inquiry / Category |
| 13 | Terms & Cond. | `nterms` / `vtermsconditions` | — | — | — | empty on every order (0 of 7,658) |
| 14 | Check List | `ncheckset` → `mstcheckset` | — (the set is a Checklist Master, §3) | — | — | the set name is not written into any remark; the order's check items are in 2.5 |
| 15 | Client Order No. | `vrefno` | `inqcs.ponumber` | PO Number | PO Number | cap 200 |
| 16 | Client Order Date | `drefdate` | `inqcs.podate` | PO Date | PO Date | |
| 17 | Prn.Order Ackn. No. | `vordacknno` | `inqcs.sono` | Principal SO Number | Principal SO Number (SO view) | 2,724 SO (e.g. `SO-176007`); no longer in the remark. verified in the DB on 23/09/2026 |
| 18 | Prn.Order Ackn. Date | `dackndate` | part of `inqcs.remark` | Remarks | Remarks tab | `Order Ackn Date: dd/MM/yyyy` |
| 19 | Order Currency | `ncurrency` → `mstcurrency` | `inqcs.currency` | Currency | Currency | by short name / name |
| 20 | (exchange rate) | `nexchangerate` | `inqcs.exchangerate` | — | — | not on the client screen, not shown for saksham |
| 21 | Remarks | `vremarks` | `inqcs.remark` (first part) | Remarks | Remarks tab | |
| 22 | Comment | `vcomment` | part of `inqcs.remark` | Remarks | Remarks tab | `Comment: …` |
| 23 | Order Status | `nordrecdstatus` → `mstfixedselection` (NOT DELIVERED / PART DELIVERY / FULFILLED) | `inqcs.status` | Status | Status | `status` (module Sales Order) by name, seeded if missing |
| 24 | Trn. Total | `ntotalamount` | — | — | Grand Total | computed: lines + Adjustments (2.3) |
| 25 | (against Quotation) | `nquote` → the migrated quotation | `inqcs.inqlink` | Quotation Reference | Reference No (link) | matched by number + branch |
| 26 | (against Inquiry) | `ninquiry` → the migrated inquiry | `inqcs.inqcode` | — | — | 3 orders |
| 27 | (delivery date of the order lines) | `trdordrc2despdate.ddespdate` | `inqcs.customertentativedate` | Customer Delivery Date | Customer Tentative Date | one row per line, but all lines of an order carry the same date (§7) |
| 28 | (More → Expenses) | `trdordrc6expenses` (type, date, amount, remark) | part of `inqcs.remark` | Remarks | Remarks tab | `Expenses: POST 27/06/2017 8600.00 <remark>; …` |
| 29 | (approval) | `bapproval` (1 on every order) | `inqcs.approvalstatus` | — | — | `''` — the value the app posts on every save (§7) |
| 30 | (office of the record) | `nofficeid` | `inqcs.branchcode`, `comcode` | Branch Name / Company Name | Company Name / Address | office → `companyaddress.code`; company 1 |
| 31 | — | `addedon` / `editedon` | `createdon` / `updatedon` | — | Created By / Updated By | Created By / Updated By | `createdby` / `updatedby` = eBizWiz `addedby` / `editedby` user (not edited → creator; unknown user → migration user). verified in the DB on 23/09/2026 |

### 2.2 Order Received Items → `inqcsdet`

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 32 | Item / Item Code | `trdordrc1items.nitem` → `mstitems` | `inqcsdet.pcode` | Product Name | Product Name / CAS No | product by Item Code (module 2, `product.casno`) |
| 33 | Quantity | `nquantity` | `inqcsdet.quantity` | Quantity | Quantity | |
| 34 | Master Rate | `nmasterrate` | `inqcsdet.price` (discounted lines) | Price | Price / Currency | already in the order currency |
| 35 | Discount % | `ndiscountperc` | `inqcsdet.discountpercent` | Discount % | — | kept on discounted lines (20,702) |
| 36 | Discount Amt. / Order Rate | `nrate` (net) | `inqcsdet.discount` = master − `nrate`; `price` = `nrate` when no discount | Discount Amount / Price | Discount Price | qty × (price − discount) = qty × `nrate` |
| 37 | Item Currency Is | `nitemcurrency` | — | — | — | the line carries the **order** currency (§7) |
| 38 | Currency Conversion Factor | `ncurrencyvalue` | — | — | — | already applied to the rates (2,501 lines ≠ 1) |
| 39 | Tax Set | `ntaxset` → `msttaxset` | `inqcsdet.prodtax` | Tax % | — | order GST % (2.3); else the Tax Set % when the line has tax (133 lines) |
| 40 | Item Total | — | — | — | Total | computed |
| 41 | Technical Set / Tech. Details | `ntechnicalset`, `trdordrc7tech` | — | — | — | 0 lines in scope (4 rows in the test offices) |
| 42 | Disp. Qty. / Status / Drop | `nquantitytotdesp`, `trdordrc2despdate.bdrop` | — | — | — | fulfilment counters, not order data (§5) |

### 2.3 Post Tax Charges → `inq_adjust`

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 43 | Post Tax Charges | `trdordrc4posttaxchgs.nmstposttaxchgs` → `mstprepostchgs.vname` | `inq_adjust.adjustname` | Adjustments row | Adjustments | charge name as printed (`Add : IGST @18%`) |
| 44 | Amount / Percentage, Percentage, Amount | `venteredinamtorper`, `ntaxpercentage`, `namount` | `inq_adjust.adjustpercent`, `adjustamount` | Adjustments row | Adjustments | PERCENTAGE → item total × %; AMOUNT as entered; zero / no amount skipped |
| 45 | (line Tax Set amounts, lines without a migrated product) | `ntaxamt`, unmapped items | `inq_adjust` | Adjustments row | Adjustments | `Tax on items (eBizWiz)` / `Other items (eBizWiz)` so Grand Total = client total |

### 2.4 Payment Terms → `inqcs.terms`

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 46 | Payment Terms, Percentage, Amount | `trdordrc3payterms.nmstpayterms` → `mstpaymentterms`, `npercentage`, `namount` | `inqcs.terms` | Terms | Term Details | `Payment Terms: ADVANCE PAYMENT 100% = 37406.00` (`; `-separated) — same rule as the quotation (04) |

### 2.5 Check List → `taskchecklist` (`module = 'SO'`)

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 47 | Check | `trdordrc5checks.ncheck` → `mstchecks` | `taskchecklist.title` | Task Check List tab | Task Check List tab | `modulecode` = order `inqcs.code`; no task record |
| 48 | Done / Done Date | `bdone`, `ddonedate` | `taskchecklist.status`, `updatedon` | | | |
| 49 | Expected Date | `dexpdate` | `taskchecklist.duedate` | | | blank → done/created date (done) or today + 7 |
| 50 | Alloted To | `nallotedto` → `mstusers` | `taskchecklist.assignto` | | | user by name |
| 51 | Remarks | `vremarks` | `taskchecklist.remark` | | | |

✚ = field added to our form / view for saksham · ✎ = standard field relabelled for saksham (§4a).

---

## 3. Masters seeded

| Master | Our table | Rows added | Source |
|---|---|---|---|
| Order Status | `status` (module Sales Order) | 6 (3 in use: NOT DELIVERED, PART DELIVERY, FULFILLED) | `mstfixedselection` |
| Order Type | `miscellaneous` (Inquiry / Order Type) | full `nordrecdtype` group | `mstfixedselection` |
| Inquiry Category | `miscellaneous` (Inquiry / Category) | 0 (795 mapped, seeded by module 3) | `mstinquirycategory` |
| Check List sets | Checklist Master: `taskchecklistmaster` (title) / `taskchecklistmasterdet` (title, days, sort) | 24 sets / 253 items, shared by all modules (seeded once, by title) | `mstcheckset` + `msdcheckset` + `mstchecks` — sets of offices 2/3/4/6 that are active or used by a migrated document |

Order Source, Pending Reason, Payment Terms and expense types are written as text, so no master is seeded for them. Pending Reason comes from `mstcallpendingreasons`; joining `mstfixedselection` on the same code gives unrelated values (YES, OPEN, WARM) - see 06 §Notes.

---

## 4. Changes made on our side

### 4a. Form / view changes (saksham-gated)

| # | Screen | Change | Kind | File | DB column |
|---|---|---|---|---|---|
| 1 | Order form | Section **Sales Order Details** (shown for SO only) | Layout | `inquiry/default.asp` (`so_detail_block`), `assets/scripts/inquiry.js` | — |
| 2 | Order form | **Order Type** dropdown | New field | same | `inqcs.ordertype` |
| 3 | Order form | **Source** dropdown | New field | same | `inqcs.sources` |
| 4 | Order form | **Category** dropdown | New field | same | `inqcs.Category` |
| 5 | Order form | **Customer Shipping Branch** (Ship To) free-hand picker beside Customer Billing Branch | New field | `inquiry/default.asp` (`so_detail_block`) | `inqcs.shippingbranch` |
| 6 | Order form | Customer Branch (= Bill To) is the free-hand picker; the customer → branch cascade is skipped for saksham | Behaviour | `inquiry/default.asp`, `assets/scripts/inquiry.js` | `inqcs.csbranch` |
| 7 | Order form | Customer Branch labelled **Customer Billing Branch** (same labels as the Sales Invoice form) | Label | `inquiry/default.asp`, `assets/scripts/inquiry.js` | `inqcs.csbranch` |
| 8 | Backend | Source + Category saved explicitly for SO (own field names, so they don't collide with the CI fields) | Backend | `app/inquiry.asp` (add + edit) | `inqcs.sources`, `Category` |
| 9 | Order view | Block with Order Type, Source, Category, **Customer Shipping Branch** (party - place - address) | New field ×4 | `inquiry/default.asp` (`so_detail_view`), `assets/scripts/inquiry.js` | as above |
| 10 | Order view | **Customer Billing Branch** row (Bill To: party - place - address) | New field | `inquiry/default.asp`, `assets/scripts/inquiry.js` | `inqcs.csbranch` |
| 11 | Order view | **Task Check List** tab (module = `SO`) | Behaviour | `inquiry/default.asp` (`showso`), `assets/scripts/inquiry.js` | `taskchecklist` |
| 12 | Backend + view/edit | Ship To and Bill To travel in the saksham trailing values of the inquiry array: ship branch code / name (17, 18), Bill To party / place / address and Ship To party / address (19–23), 24 values in total; positions 91/92 (Zinq / lopa ship branch) are not used for saksham | Backend + Behaviour | `app/inquiry.asp`, `assets/scripts/inquiry.js` | `inqcs.csbranch`, `shippingbranch` |

**New fields on the form: 4** (Order Type, Source, Category, Customer Shipping Branch) · **on the view: 5** (Order Type, Source, Category, Customer Shipping Branch, Customer Billing Branch) · labels: 1 · tabs: Task Check List · behaviour / backend: 3.

### 4b. Database changes (ALTER)

None for the Sales Order. Bill To / Ship To are branch references (`csbranch`, `shippingbranch`); the widened `inqcs.bilingaddress` / `deliveryaddress` columns in `00_ALTERS_run_before_exe.sql` are not written by this module.

---

## 5. Not migrated

| Client UI | Client DB | Rows | Why |
|---|---|---|---|
| Order From = FROM OFFICE / CALL MADE | `nordrecdfrom`, `nfromoffice`, `nfromuser` | 3 orders | the order is migrated as a customer order; no inter-office order on our side |
| Terms & Cond. | `nterms`, `vtermsconditions` | 0 | empty in the client data |
| Select Letterhead to print | `vletterhead` | 4,253 | print option chosen when printing |
| Trn. Total / Item Total / Total Discount / Total Tax | `ntotalamount`, `nitemtotal`, `ntotaldiscount`, `ntotaltaxamt` | — | derived from the lines + Adjustments |
| Disp. Qty. / Status / Drop, despatched & rejected quantity | `trdordrc2despdate.nquantity`/`bdrop`, `nquantitytotdesp`, `nquantitytotreject`, `nordplcdqty` | 21,344 / 25,320 / 30,137 | fulfilment counters; on our side delivery is the Sales Invoice (07) and the purchase link (06) |
| Technical Set | `ntechnicalset`, `trdordrc7tech` | 0 in scope | no technical-set field on our order line |
| Item Currency / Conversion Factor | `nitemcurrency`, `ncurrencyvalue` | 14,435 / 2,501 lines | already applied to the rates (§7) |
| Scheme, advance / cheque, commission, entry-tax and octroi flags, business group, OS reason, billing from | `nscheme*`, `nadvance`, `vchqno`, `dchqdate`, `nbank`, `ncommamt`, `nthirdparty`, `ncommtosalesman`, `bentrytax`, `bformc`, `bformd`, `btaxinclusive`, `broadpermit`, `boct*`, `nbusigroup`, `nosreason`, `nbillingfrom`, `bdod`, `bcod`, `bcashadvance`, `bneworder`, `bupgradation` | 0 everywhere | not used by this client |
| SP number | `vspnumber` | 0 | empty |
| Upload Doc (More menu) | `trhdoc` | — | DMS is out of scope (user's decision) |

---

## 6. Verification (latest run)

Run ALL, 0 errors.

| Check | Client | Ours |
|---|---|---|
| Orders | 7,658 | **7,658** `inqcs` SO (7,659 incl. template) |
| Customer | 7,654 with a party | **7,654** resolved, 0 unresolved |
| Lines | 35,747 (all with an item) | **35,731** `inqcsdet` = 35,747 − 2 (item not migrated) − 14 lines (₹36,819.95) on 2 orders whose header no longer exists in eBizWiz; value of the migrated lines = client exactly |
| Line value | qty × `nrate` | 0 differences |
| Line currency | 23,824 lines whose item currency ≠ order currency | **0** lines with a currency different from their order |
| Quotation link | 7,214 orders against a quotation | **7,214** `inqlink` |
| Inquiry link | 3 | **3** `inqcode` |
| Status | NOT DELIVERED 2,836 · PART DELIVERY 302 · FULFILLED 4,464 · blank 56 | same counts |
| Bill To | 7,440 orders with a Bill To, 670 of them a different party | **669** `csbranch` on the other party's branch + 1 whose Bill To party no longer exists in the client (falls back to the ordering party) |
| Ship To | 7,458 | **7,456** correct party (2 source parties no longer exist) |
| Payment Terms | 4,548 rows on 4,023 orders | **4,023** `inqcs.terms` |
| Delivery date | 4,954 orders with a despatch date | **4,954** `customertentativedate` |
| Expenses in remark | 311 orders (313 rows) | **311** |
| Approval status | — | `''` on all **7,658** SO (and all 193,337 CI) |
| Check list | 149,662 items on 4,702 orders, 128,828 done | **149,662** `taskchecklist` SO, **128,828** done |
| Check List sets | sets of offices 2/3/4/6, active or used | Checklist Master **24** sets / **253** items; `Check List:` in SO remarks **0** |
| Tax / charges | 3,186 orders with GST, line Tax Set on 113 | GST % on 3,157, `inq_adjust` 14,265 rows, discount on 20,690 lines |
| Grand total | 7,658 | = client total except 2 orders whose own header doesn't match their lines |

---

## 7. Notes

- **Bill To.** The client's Bill To is a party of its own and differs from the ordering party on 670 orders. Our Customer Billing Branch field is the free-hand picker (`combo=freebranch`, `app/selectjson.asp`), which lists **any** contact's branch as "party - place" and stores only `mltaddress.code`, so another party's branch is a valid Bill To — the same rule the Sales Invoice (07) uses. Blank Bill To falls back to the ordering party's branch.
- **Bill To / Ship To labels.** Form and view use the Sales Invoice form labels **Customer Billing Branch** (Bill To) and **Customer Shipping Branch** (Ship To); both view rows show party - place - address.
- **Ship To array positions.** Positions 91/92 of the inquiry array hold the ship branch only for Zinq / lopa; for saksham they carry `requisiteno` / `courier_no`. The saksham view and edit form therefore read Ship To (and the Bill To / Ship To display values) from the saksham trailing values (§4a #12), so no other company's positions move.
- **Delivery date.** eBizWiz keeps one despatch row per order line with the same quantity as the line, and every line of an order carries the same date (4,954 of 4,954 orders with a date). Our SO form has the date on the header ("Customer Delivery Date" → `inqcs.customertentativedate`); there is no line-level date field for saksham (the `Target Delivery Dt` / `Offer Qty` line columns are Akpowertech-only). So one date per order.
- **Line currency.** As in the quotation (04), eBizWiz converts the price into the order currency when the line is entered, so the line carries the order currency; the item currency would show the wrong currency name next to the price and would make the form's Convert Currency action multiply the price again.
- **Check lists.** The order's check items are in the Task Check List tab (`taskchecklist`); the eBizWiz check sets with their items are the standard Checklist Master (§3). The set name is not written into the remark.
- **Approval.** `inqcs.approvalstatus` defaults to `Pending`, but the app posts `''` on every save, and the SO view has no approval gate at all (the gate is inside the CQ branch). The client's order screen has no approval step either, so `''`.
- **Print / Email** is hidden on the SO view for every company (`inquiry.js`, sales-order tab), so the client's "Select Letterhead to print" has no counterpart.
- **Order Expenses** (More → Expenses, 313 rows / ₹87.5 lakh, mostly commission notes) have no grid on our order; they are kept as labelled text in Remarks. This is separate from the client's *Master → Expenses* module, which has no real data (mapping 15).
- **Charges formula** and the `inq_adjust` rules are the same as the quotation (04 §7): a PERCENTAGE charge is on the item total, AMOUNT charges as entered, and the saksham view totals lines + Adjustments without any line tax.
