# 04. Quotation Entry → Customer Quotation

Format: see `00_MAPPING_FORMAT.md`.

## 1. Overview

| | Client (eBizWiz) | Ours (EdifyBiz) |
|---|---|---|
| Screen / menu | **Quotation Entry** — header, *Quotation Items*, *Quotation Post Tax Charges*, *Quotation Payment Terms*, check list | **Inquiry** module, document type `CQ` (Customer Quotation) — form (*Quotation Details* section), view, Product Details (lines + Adjustments + Term Details), Remarks, Task Check List tab |
| Tables | `trhquote` (header), `trdquote1items` (items), `trdquote3posttaxchgs` (post-tax charges), `trdquote2payterms` (payment terms), `trdquote5checks` (check list); masters `mstfixedselection` (Quote Type, Quote Status), `mstinquirycategory`, `mstorderlostreason`, `mstcurrency`, `mstprepostchgs`, `mstpaymentterms`, `msttermset`, `mstcheckset` + `msdcheckset`, `mstchecks`, `msttaxset`, `mstusers` | `inqcs` (`cors = 'CQ'`), `inqcsdet`, `inq_adjust`, `taskchecklist` (`module = 'CQ'`); masters `status` (Quotation), `miscellaneous` (Inquiry / Quote Type, Loss Reason, Category), `currency`, `taskchecklistmaster` + `taskchecklistmasterdet` (Checklist Master) |
| Code | — | form + view `inquiry/default.asp`, JS `assets/scripts/inquiry.js`, backend `app/inquiry.asp` (saved by field name through `SqlOperation`), check list component `assets/scripts/taskchecklistfill.js` |
| Exe | — | module **4. Customer Quotation** — class `CustomerQuotation` in `Program.cs` (after modules 1–3); check sets seeded by `Shared.EnsureChecklistMasters` (see 03) |
| Scope | offices 2, 3, 4, 6 (1 and 5 are WinMax test offices) | |
| Status | | migrated and verified |

AMC quotations (eBizWiz *AMC Quotation*, module 9) are also `inqcs` `CQ` rows, told apart by `modulename = 'AMC Contract'`.

---

## 2. Field mapping

### 2.1 Header → `inqcs` (`cors = 'CQ'`)

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 1 | Trn. No. | `trhquote.vtrnprefix` + `ntrnno` | `inqcs.inqref` | Quotation Reference | page title / Reference Number | `ntrnno` is decimal(,2): `.00` dropped (`QT38012`), a revision keeps its suffix (`QT37439.10`) |
| 2 | Trn. Date | `dtrndate` | `inqcs.inqdate` | Quotation Date | Quotation Date | |
| 3 | Party | `nparty` → `mstparty` | `inqcs.cscode` | Customer | Customer Name | party → contact (module 1) |
| 4 | — | — | `inqcs.csbranch` | **Customer Billing Branch** | **Customer Billing Branch** ✚ | the contact's default `mltaddress`; the view shows party - place - address |
| 5 | Party Contact | `npartycontact` → `msdparty` | `inqcs.cperson` | Contact Person | Contact Person | `mltcontact.code` of that person |
| 6 | Quote Type | `nquotetype` → `mstfixedselection` | `inqcs.quotetype` | **Quote Type** ✚ | **Quote Type** ✚ | `miscellaneous` Inquiry / Quote Type by name; full client group seeded |
| 7 | Inquiry No. | `ninquiry` → `trhinqry` | `inqcs.inqlink` | Inquiry Reference | Reference No (link) | the migrated CI row with the same number in the same branch |
| 8 | Sales Person | `nsalesman` → `mstusers` | `inqcs.executive` | Executive | Executive Name | user by name; Admin when not found |
| 9 | Follow Up Date | `dfollowupdate` | `inqcs.followupdate` | **Follow Up Date** ✚ | **Follow Up Date** ✚ | own column (ALTER, §4b) — not `validdate` |
| 10 | Quote Currency | `ncurrency` → `mstcurrency` | `inqcs.currency` | Currency | Currency | matched by short name / name |
| 11 | (exchange rate) | `nexchangerate` | `inqcs.exchangerate` | — | — | not on the client screen; filled on 1 quotation; not shown for saksham |
| 12 | Validity | `vvalidity` (free text) | part of `inqcs.remark` | Remarks | Remarks tab | `Validity: 90 Days` |
| 13 | Quote Status | `nquotestatus` → `mstfixedselection` (OPEN, LOST, REVISED, WON) | `inqcs.status` | Status | Status | `status` (module Quotation) by name, seeded if missing |
| 14 | Lost Reason | `nlostreasons` → `mstorderlostreason` | `inqcs.lossreason` | **Loss Reason** ✚ | **Loss Reason** ✚ | `miscellaneous` Inquiry / Loss Reason (seeded by module 3) |
| 15 | Terms & Cond. | `nterms` → `msttermset` | part of `inqcs.terms` | Terms | Term Details | `Terms & Cond.: ESCO Standard Terms and Conditions` |
| 16 | Check List | `ncheckset` → `mstcheckset` | — (items → `taskchecklist`, 2.5) | Task Check List tab | Task Check List tab | the set name is not written into any remark; the set with its items is a Checklist Master (§3) |
| 17 | Ref. No. | `vrefno` | `inqcs.enqrefno` | **Enquiry/Tender Ref No** ✚ | **Ref No** ✚ | cap 200 |
| 18 | Ref. Date | `drefdate` | `inqcs.enqrefdate` | **Ref Date** ✚ | **Ref Date** ✚ | |
| 19 | Remarks | `vremarks` | `inqcs.remark` (first part) | Remarks | Remarks tab | |
| 20 | Comment | `vcomment` | part of `inqcs.remark` | Remarks | Remarks tab | `Comment: …` |
| 21 | Signatory | `nsignatory` → `mstusers` | part of `inqcs.remark` | Remarks | Remarks tab | `Signatory: <user name>` — no signatory field on our quotation |
| 22 | Trn. Total | — | — | — | Grand Total | computed: lines + Adjustments (2.3) |
| 23 | Inquiry Category | `ninquirycategory` → `mstinquirycategory` | `inqcs.Category` | **Category** ✚ | **Category** ✚ | `miscellaneous` Inquiry / Category |
| 24 | (approval) | `bapproval` (1 on every quotation) | `inqcs.approvalstatus` | — | — | `Approved` — the value the app's Approve action writes |
| 25 | (office of the record) | `nofficeid` | `inqcs.branchcode`, `comcode` | Branch Name / Company Name | Company Name / Address | office → `companyaddress.code`; company 1 |
| 26 | — | `addedon` / `editedon` | `createdon` / `updatedon` | — | — | `createdby` / `updatedby` = migration user |

### 2.2 Quotation Items → `inqcsdet`

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 27 | Item Name / Item Code | `trdquote1items.nitem` → `mstitems` | `inqcsdet.pcode` | Product | Product Name / CAS No | product by Item Code (module 2, `product.casno`) |
| 28 | Quantity | `nquantity` | `inqcsdet.quantity` | Quantity | Quantity | |
| 29 | Master Rate | `nmasterrate` | `inqcsdet.price` (discounted lines) | Price | Price / Currency | already in the quote currency |
| 30 | Discount % | `ndiscountperc` | `inqcsdet.discountpercent` | Discount % | — | kept on discounted lines |
| 31 | Discount Amt. / Quote Rate | `nrate` (net) | `inqcsdet.discount` = master − `nrate`; `price` = `nrate` when no discount | Discount Amount / Price | Discount Price (qty × discount) | qty × (price − discount) = qty × `nrate` |
| 32 | Item Currency Is | `nitemcurrency` | — | — | — | the line carries the **quotation** currency (`inqcs.currency`); item currency only when the quotation has none |
| 33 | Tax Set | `ntaxset` → `msttaxset` | `inqcsdet.prodtax` | Tax % | — | quotation GST % (2.3); else the Tax Set % when the line has tax |
| 34 | Item Total | — | — | — | Total | computed |

### 2.3 Quotation Post Tax Charges → `inq_adjust`

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 35 | Post Tax Charges | `trdquote3posttaxchgs.nmstposttaxchgs` → `mstprepostchgs.vname` | `inq_adjust.adjustname` | Adjustments row | Adjustments | charge name as printed (`Add : IGST @18%`) |
| 36 | Amount / Percentage, Percentage, Amount | `venteredinamtorper`, `ntaxpercentage`, `namount` | `inq_adjust.adjustpercent`, `adjustamount` | Adjustments row | Adjustments | PERCENTAGE → item total × %; AMOUNT as entered; a charge with no amount (e.g. `TOTAL F. O. R. DESTINATION`) is not written |
| 37 | Apply to Item Total Only / Apply to Running Post Tax Total Only | `bapplyonitemtotalonly`, `brunningposttax` | — | — | — | % is always on the item total (matches the client total, §7) |
| 38 | (line Tax Set amounts, lines without a migrated product) | `trdquote1items.ntaxamt`, blank items | `inq_adjust` | Adjustments row | Adjustments | `Tax on items (eBizWiz)` / `Other items (eBizWiz)` so Grand Total = client total |

### 2.4 Quotation Payment Terms → `inqcs.terms`

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 39 | Payment Terms, Percentage, Amount | `trdquote2payterms.nmstpayterms` → `mstpaymentterms`, `npercentage`, `namount` | part of `inqcs.terms` | Terms | Term Details | `Payment Terms: ADVANCE PAYMENT 100% = 227539.00` (up to 4 per quotation, separated by `;`), before Terms & Cond. |

### 2.5 Check List → `taskchecklist` (`module = 'CQ'`)

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 40 | Check | `trdquote5checks.ncheck` → `mstchecks` | `taskchecklist.title` | Task Check List tab | Task Check List tab | `modulecode` = quotation `inqcs.code` |
| 41 | Done / Done Date | `bdone`, `ddonedate` | `taskchecklist.status`, `updatedon` | tab | tab | |
| 42 | Expected Date | `dexpdate` | `taskchecklist.duedate` | tab | tab | blank → done date / added date (done) or today + 7 |
| 43 | Allotted To | `nallotedto` → `mstusers` | `taskchecklist.assignto` | tab | tab | user by name |
| 44 | Remarks | `vremarks` | `taskchecklist.remark` | tab | tab | |

✚ = field added to our form / view for saksham (§4a).

---

## 3. Masters seeded

| Master | Our table | Rows added | Source |
|---|---|---|---|
| Quote Status | `status` (module Quotation) | 3 (4 mapped; OPEN → existing `Open`) | `mstfixedselection` values used |
| Quote Type | `miscellaneous` (Inquiry / Quote Type) | 8 (4 in use) | full `nquotetype` group |
| Lost Reason | `miscellaneous` (Inquiry / Loss Reason) | 0 (54 mapped, seeded by module 3) | `mstorderlostreason` |
| Inquiry Category | `miscellaneous` (Inquiry / Category) | 0 (795 mapped, seeded by module 3) | `mstinquirycategory` |
| Checklist Master | `taskchecklistmaster` / `taskchecklistmasterdet` | 0 here (24 sets / 253 items, seeded once by `Shared.EnsureChecklistMasters`, see 03) | `mstcheckset` + `msdcheckset` |

Payment Terms, Terms set and Signatory names are written as text, so no master is seeded for them.

---

## 4. Changes made on our side

### 4a. Form / view changes (saksham-gated)

| # | Screen | Change | Kind | File | DB column |
|---|---|---|---|---|---|
| 1 | Quotation form | New section **Quotation Details** (shown for CQ only) | Layout | `inquiry/default.asp` (`cq_detail_block`), `assets/scripts/inquiry.js` (block toggle) | — |
| 2 | Quotation form | **Quote Type** dropdown + quick-add | New field | same | `inqcs.quotetype` |
| 3 | Quotation form | **Loss Reason** dropdown + quick-add | New field | same | `inqcs.lossreason` |
| 4 | Quotation form | **Enquiry/Tender Ref No** | New field | same | `inqcs.enqrefno` |
| 5 | Quotation form | **Ref Date** | New field | same | `inqcs.enqrefdate` |
| 6 | Quotation form | **Follow Up Date** | New field | same | `inqcs.followupdate` |
| 7 | Quotation form | **Category** dropdown + quick-add | New field | same | `inqcs.Category` |
| 8 | Quotation form (edit) | the six fields filled from the record | Behaviour | `assets/scripts/inquiry.js` | — |
| 9 | Quotation view | New block with Quote Type, Currency, Ref No, Ref Date, Loss Reason, Follow Up Date, Category | New field ×6 (+ Currency repeated) | `inquiry/default.asp` (`cq_detail_view`), `assets/scripts/inquiry.js` | as above |
| 10 | Backend | the quotation array returns the names / codes of the new fields | Backend | `app/inquiry.asp` | — |
| 11 | Quotation view | **Task Check List** tab shown for CQ (`aInquiry[16] == 'CQ'`, tag `CQ`) | Behaviour | `assets/scripts/inquiry.js` | `taskchecklist` |
| 12 | Quotation form (add / edit) | Bill To label **Customer Billing Branch** (shared inquiry form, see 03) | Label | `inquiry/default.asp` (`#custbranch`), `assets/scripts/inquiry.js` | `inqcs.csbranch` |
| 13 | Quotation view | New row **Customer Billing Branch**: party - place - address of the Bill To branch (shared inquiry view, see 03) | New field | `inquiry/default.asp` (`inquiry_view_saks_billto`), `app/inquiry.asp`, `assets/scripts/inquiry.js` | `inqcs.csbranch` |

**New fields on the form: 6** (Quote Type, Loss Reason, Enquiry/Tender Ref No, Ref Date, Follow Up Date, Category) · **on the view: 7** (the six + Customer Billing Branch) · labels: 1 (Customer Billing Branch) · tabs: Task Check List · behaviour / backend: 3.

### 4b. Database changes (ALTER)

From `00_ALTERS_run_before_exe.sql`:

```sql
IF COL_LENGTH('inqcs','quotetype')    IS NULL ALTER TABLE inqcs ADD quotetype    int NULL;
IF COL_LENGTH('inqcs','enqrefno')     IS NULL ALTER TABLE inqcs ADD enqrefno     nvarchar(200) NULL;
IF COL_LENGTH('inqcs','enqrefdate')   IS NULL ALTER TABLE inqcs ADD enqrefdate   datetime NULL;
IF COL_LENGTH('inqcs','followupdate') IS NULL ALTER TABLE inqcs ADD followupdate datetime NULL;
```

---

## 5. Not migrated

| Client UI | Client DB | Rows | Why |
|---|---|---|---|
| Quote For Item Groups / Item Group Heading | `bitemset`, `trdquote1items.nitemset` | 10 / 1 | screen grouping option; no item groups on our quotation |
| Show Items Of Selected Currency | `bshowcurrency` | 491 | product search filter, not data |
| Currency Conversion Factor | `trdquote1items.ncurrencyvalue` | 96,067 lines ≠ 1 | already applied to the rates |
| Technical Set | `trdquote1items.ntechnicalset`, `trdquote4tech` | 3 lines / 15 rows | no technical-set field on our quotation line |
| Line description | `trdquote1items.vdescription` | 3 lines | not on the Quotation Items screen |
| Print with Price / Print with Terms / Select Letterhead to print | `bPrintwithPrice`, `bPrintwithTerms`, `vletterHead` | — | print options chosen when printing |
| Header discount | `trhquote.ndiscount` | 120 | information only (sum of line discounts), not deducted from the client total |
| Approved by / on | `approvedby`, `approvedon` | 2 | too few to matter; status is `Approved` |
| Terms & Cond. text | `vtermsconditions` | 0 | empty in the client data |

---

## 6. Verification (latest run — 0 errors)

| Check | Client | Ours |
|---|---|---|
| Quotations | 110,493 | **110,493** `inqcs` CQ (110,494 incl. template) + 8,758 AMC quotations |
| Customer | 110,492 with a party (1 without) | **110,492** resolved, 0 unresolved |
| Contact person | 109,755 set, 23 point to a blank / missing client person | **109,732** |
| Sales person | 110,328 | **110,328** real users |
| Quote Type | Against Inquiry - Sales 95,939 · Against Inquiry - Service 14,478 · Fresh - Sales 60 · Fresh - Service 16 | same counts |
| Status | OPEN 35,464 · LOST 49,060 · REVISED 16,237 · WON 9,732 | same counts |
| Number | 92,202 `.00` numbers, 18,291 revisions | **0** `inqref` ending in `.00`; **18,291** keep `.10`–`.90` |
| Inquiry link | 110,379 with an inquiry | **110,379** `inqlink` |
| Follow Up Date / Currency / Lost Reason / Ref No / Ref Date / Category | 110,489 / 110,492 / 33,993 / 109,070 / 110,426 / 29,669 | same counts |
| Approval | `bapproval` = 1 on 110,493 | **110,493** `approvalstatus = 'Approved'` |
| Lines | 473,651 (473,650 with an item) | **473,641** `inqcsdet` (9 item not migrated) |
| Line currency | 113,197 CQ + 23,824 SO lines whose item currency ≠ document currency | **0** lines with a currency different from their document (CQ and SO) |
| Line value | qty × `nrate` | 0 differences on all quotations |
| Grand total | 109,567 quotations with a client total | differences only where the client's own header ≠ its lines or charges (§7) |
| Payment Terms + Terms & Cond. | 35,001 with payment terms, 3,916 with a term set, 35,358 with either | `inqcs.terms`: **35,001** + **3,916** = **35,358** |
| Check list | 271,700 items on 22,456 quotations, 233,925 done | **271,700** `taskchecklist` CQ (297,533 with the 25,833 AMC ones) |
| Signatory in remark | 683 | **683** |
| Links from SO / Sales Invoice | SO → quotation 7,214; invoice → SO/CQ 3,327 | same counts (links built with `Shared.QuoteRef`) |

---

## 7. Notes

- **Number format.** eBizWiz stores the quotation number as decimal with 2 places (`ntrnno` 38012.00, 37439.10). Checked in the client DB, not from screenshots (those come from the live server): wherever the client's users typed a quotation number (inquiry remarks, check-list remarks, stock-in ref no, document names, call remarks) the original never has `.00` (`QT869`, `QT15563`, `QT22021`) and a revision always has its two digits (`QT1366.10`, `QT9456.20`, `QT21261.60`, `QT403.50`); the only `.00` matches are unrelated text such as `LF5.001.000.pdf`. The exe uses one helper (`Shared.QuoteRef`) for the quotation and for the Sales Order / Sales Invoice links, so those links match. Our revision tab (`parentcode`) is Akpowertech-only, so revisions stay separate quotations.
- **Line currency.** eBizWiz converts the item price into the quotation currency when the line is entered (e.g. 3,000 USD × 94 = 2,82,000 INR master rate), so `nmasterrate` / `nrate` are quote-currency values. Our view shows "Price / Currency" per line, and the form's Convert Currency action multiplies lines whose currency differs from the header — so the line carries the quotation currency. Sales Order (05) follows the same rule.
- **Approval.** `inqcs.approvalstatus` defaults to `Pending` in the database, and the app itself posts `''` on every save (`inquiry.js` `txtapprovalstatus ""`), so `Pending` never comes from the app — Customer Inquiry (03) and Sales Order (05) are therefore migrated with `''`. The quotation is the exception: for CQ the Print / Email menu and the Documents tab appear only when the quotation is Approved (`inquiry.js` approval checks), and Pending quotations are listed under Unapproved Quotation — hence `Approved`, same as AMC Quotation.
- **Terms.** Our quotation has one Terms field (form "Terms", view "Term Details" below the products). The Terms & Condition tab with separate payment-term fields is Akpowertech-only, so the client's Payment Terms grid and term set name are written there as text.
- **Validity** stays in Remarks: the client field is free text with 866 different values ("90 Days", "Three months from the date of opening Tender" …), so it cannot become Valid Date.
- **Charges formula.** A PERCENTAGE charge is always on the item total (qty × `nrate` of all lines); AMOUNT charges as entered. The saksham view shows no line tax and totals lines + Adjustments, so GST goes to `inq_adjust` as the client printed it and `inqcsdet.prodtax` only keeps the GST %. Remaining total differences come from the client data itself: quotations without lines, headers that don't match their own lines, and totals that don't follow the client's own charges (e.g. QT25953.20: items 1,42,000 + 4,260 = 1,46,260, client total 1,42,426).
- **Check list** uses the standard Task Check List component with tag `CQ`, same as AMC Quotation (`modulename = 'AMC Contract'`) and Sales Order (`SO`). The check-set name is not written into the remark; the eBizWiz check sets are the Checklist Master (see 03).
- **Bill To label:** for saksham the quotation form and view use the Sales Invoice label **Customer Billing Branch** for `inqcs.csbranch` (see 03).
