# 03. Inquiry Entry → Customer Inquiry

Format: see `00_MAPPING_FORMAT.md`.

## 1. Overview

| | Client (eBizWiz) | Ours (EdifyBiz) |
|---|---|---|
| Screen / menu | **Inquiry → Inquiry Entry / Offer Request Entry** — header, *1st Action Details*, *Action Taken Detail*, *Next Action To Be Taken*, *Inquiry Progress Details*, *Inquiry Items* | **Inquiry** module, document type `CI` (Customer Inquiry) — form, view, Items, Follow Up, Task Check List tab |
| Tables | `trhinqry` (header), `trdinqry1prods` (items), `trdinqry2actions` (actions), `trdinqry3competitor` (competitors), `trdinqry4checks` (check list); masters `mstfixedselection` (Inquiry By, Type, Status, Priority, Inquiry Value), `mstinquirycategory`, `mstinquirysource`, `mstsalesstage`, `mstorderlostreason`, `mstprobability`, `mstcheckset` + `msdcheckset`, `mstchecks`, `mstactiontobetaken`, `mstactiontaken`, `trhcampa` | `inqcs` (`cors = 'CI'`), `inqcsdet`, `followup` (`module = 'INQ'`), `taskchecklist` (`module = 'CI'`); masters `status` (Inquiry), `miscellaneous` (Inquiry / Category, Sales Stage, Loss Reason, Call Type, Inquiry Source, Action Type), `followupremarks`, `priority`, `taskchecklistmaster` + `taskchecklistmasterdet` (Checklist Master) |
| Code | — | form + view `inquiry/default.asp`, JS `assets/scripts/inquiry.js`, backend `app/inquiry.asp`, check list component `assets/scripts/taskchecklistfill.js` |
| Exe | — | module **3. Customer Inquiry** — class `CustomerInquiry` (+ nested `Followup`) in `Program.cs`; check sets seeded by `Shared.EnsureChecklistMasters` before the module runs |
| Scope | offices 2, 3, 4, 6 (1 and 5 are WinMax test offices) | |
| Status | | migrated and verified |

---

## 2. Field mapping

### 2.1 Header → `inqcs` (`cors = 'CI'`)

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 1 | Trn. No. | `trhinqry.vtrnprefix` + `ntrnno` | `inqcs.inqref` | Auto Generated Number (read-only) | page title | e.g. `IQ70845` |
| 2 | Trn. Date | `dtrndate` | `inqcs.inqdate` | Inquiry Date | Inquiry Date | |
| 3 | Party | `nparty` → `mstparty` | `inqcs.cscode` | Customer | Contact Name | party → contact (module 1) |
| 4 | — | — | `inqcs.csbranch` | **Customer Billing Branch** | **Customer Billing Branch** ✚ | the contact's default `mltaddress`; the view shows party - place - address |
| 5 | Contact Person | `npartycontact` → `msdparty` | `inqcs.cperson` | Contact Person | Contact Person | `mltcontact.code` of that person |
| 6 | Sales Person | `nsalesman` → `mstusers` | `inqcs.executive` | Executive | Executive Name | user by name; Admin when not found |
| 7 | Inquiry By | `ninquiryby` → `mstfixedselection` (CALL MADE …) | `inqcs.calltype` | **Inquiry By** ✚ | **Inquiry By** ✚ | `miscellaneous` Inquiry / Call Type |
| 8 | Category | `ninquirycategory` → `mstinquirycategory` | `inqcs.Category` | **Category** ✚ | **Category** ✚ | `miscellaneous` Inquiry / Category |
| 9 | Inquiry Details | `vinquirydetails` | `inqcs.remark` (first line) | Remark | Remark section | |
| 10 | Campaign | `ntrhcampa` → `trhcampa` | part of `inqcs.remark` | Remark | Remark section | `Campaign: CA5 - <name>` (6 inquiries) |
| 11 | Source | `ninquirysource` → `mstinquirysource` | `inqcs.sources` | **Source** ✚ | **Source** ✚ | name as text; full master seeded |
| 12 | Type (DSR - NO ACTION NEEDED / DSR - ACTION NEEDED / SALES LEAD) | `nactionType` → `mstfixedselection` | `inqcs.types` | **Type** ✚ | **Type** ✚ | name as text |
| 13 | (office of the record) | `nofficeid` | `inqcs.branchcode`, `comcode` | — | — | office → `companyaddress.code`; company 1 |

### 2.2 Inquiry Progress Details → `inqcs`

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 14 | Sales Stage | `nsalesstage` → `mstsalesstage` | `inqcs.stage` | **Sales Stage** ✚ | **Sales Stage** ✚ | full master seeded |
| 15 | Status | `ninquirystatus` → `mstfixedselection` (OPEN, DSR CLOSED …) | `inqcs.status` | Status | Status | `status` (module Inquiry) by name, seeded if missing |
| 16 | Approx. Close Date | `dclosingdate` | `inqcs.validdate` | Valid Date | Valid Date | |
| 17 | Lost Reason | `nlostreasons` → `mstorderlostreason` | `inqcs.lossreason` | **Lost Reason** ✚ | **Loss Reason** ✚ | full master seeded |
| 18 | Check List | `ncheckset` → `mstcheckset` | — (items → `taskchecklist`, 2.5) | Task Check List tab | Task Check List tab | the set name is not written into any remark; the set with its items is seeded as a Checklist Master (§3) |
| 19 | Probability % of Getting The Order | `nprobability` → `mstprobability` | part of `inqcs.remark` | Remark | Remark section | `Probability % of Getting The Order: 25% in Our Favor` |
| 20 | Inquiry Value | `ninqvalue` → `mstfixedselection` | part of `inqcs.remark` | Remark | Remark section | `Inquiry Value: Upto Rs. 5 Lacs`; "NONE" is not written |
| 21 | Quote No. / Quote Value | `nquotation`, `nquotedvalue` | — | — | — | not stored on the inquiry; the quotation (module 4) points back with `inqcs.inqlink` |
| 22 | Order No. / Order Value | `norderno`, `nordwonvalue` | — | — | — | filled by the order, not entered |
| 23 | Remarks | `vremarks` | part of `inqcs.remark` | Remark | Remark section | after Inquiry Details |
| 24 | Comment | `vcomment` | part of `inqcs.remark` | Remark | Remark section | `Comment: …` |
| 25 | — | `addedon` / `editedon` | `createdon` / `updatedon` | — | — | `createdby` / `updatedby` = migration user |

### 2.3 Inquiry Items → `inqcsdet`

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 26 | Item Code / Item Name | `trdinqry1prods.nitem` → `mstitems` | `inqcsdet.pcode` | Product | Items grid | product by Item Code (module 2, `product.casno`) |
| 27 | Quantity | `nquantity` | `inqcsdet.quantity` | Quantity | Items grid | |

### 2.4 1st Action / Action Taken / Next Action → `followup` (`module = 'INQ'`, one row per client action)

| # | Client UI | Client DB | Our DB | Our Form (saksham action block) | Our View | Rule |
|---|---|---|---|---|---|---|
| 28 | Purpose | `trdinqry2actions.nactiontobetaken` → `mstactiontobetaken` | `followup.purpose` | **Purpose** ✚ | Follow Up tab | name as text |
| 29 | Date | `dactiontobedate` | `followup.nextfollowup` | **Date** ✚ | Follow Up tab | |
| 30 | Priority | `npriority` → `mstfixedselection` | `followup.priority` | **Priority** ✚ | Follow Up tab | `priority` master by name |
| 31 | Purpose Remarks | `vactiontoberemarks` | part of `followup.remarks` | **Remarks** ✚ | Follow Up tab | `Purpose Remark: …`, only when it differs from the Action Taken remark |
| 32 | Action Taken | `nactiontaken` → `mstactiontaken` | `followup.remarkscode` → `followupremarks` | **Action Taken** ✚ | Follow Up tab | Purpose name when no action was taken |
| 33 | — | (action text) | `followup.followType` | — | Follow Up tab | keyword rule: Meeting / Call / SMS / Fax / E-Mail / Others |
| 34 | Action Taken Date | `dactiondate` | `followup.lastfollowup` | **Action Taken Date** ✚ | Follow Up tab | |
| 35 | Action Taken Remarks | `vremarks` | `followup.remarks` | **Remarks** ✚ | Follow Up tab | |
| 36 | Person Contacted | `npartycontact1` → `msdparty` | `followup.person`, `followup.pcode` | **Person Contacted** ✚ | Follow Up tab | `mltcontact.code` |
| 37 | Sales Person | `nsalesman` → `mstusers` | `followup.createdby` | **Sales Person** ✚ (next action) | Follow Up tab | the follow-up's owner |
| 38 | (inquiry) | `ninquiry` | `followup.modulecode`, `followup.ccode` | — | — | inquiry code + its customer |
| 39 | Next Action To Be Taken / Action To Be Taken Date / Party Contact / Action To be Taken Remarks | the next `trdinqry2actions` row of the same inquiry | its own `followup` row (same rules 28-37) | **Next Action** block ✚ | Follow Up tab | eBizWiz stores the next action as a separate action row |

### 2.5 Check List → `taskchecklist` (`module = 'CI'`, `modulecode` = inquiry code)

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 40 | Check | `trdinqry4checks.ncheck` → `mstchecks` | `taskchecklist.title` | Task Check List tab ✚ | Task Check List tab ✚ | same component and rules as Sales Order / Quotation |
| 41 | Done / Done Date | `bdone`, `ddonedate` | `status` (1 / 0), `updatedon` | tab | tab | |
| 42 | Expected Date | `dexpdate` | `duedate` | tab | tab | none → done date / created date if done, else today + 7 |
| 43 | Allotted To | `nallotedto` → `mstusers` | `assignto` | tab | tab | user by name |
| 44 | Remarks | `vremarks` | `remark` | tab | tab | |

### 2.6 Competitor details → `inqcs.remark`

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 45 | Competitor / Product / Feature / Price / Order Won | `trdinqry3competitor.ncompetitor` → `mstparty`, `vproduct`, `vfeature`, `npricewon`, `borderwon` | part of `inqcs.remark` | Remark | Remark section | `Competitor: <party> (Product: …, Feature: …, Price: …, Order Won: Yes/No)`; several competitors of one inquiry separated by `;` |

✚ = field / tab added to our form or view for saksham (§4a).

---

## 3. Masters seeded

| Master | Our table | Rows added | Source |
|---|---|---|---|
| Sales Stage | `miscellaneous` (Inquiry / Sales Stage) | 13 (full master; 4 inactive in eBizWiz → `active = 0`) | `mstsalesstage` |
| Loss Reason | `miscellaneous` (Inquiry / Loss Reason) | 60 (full master; 6 inactive → `active = 0`) | `mstorderlostreason` |
| Inquiry By | `miscellaneous` (Inquiry / Call Type) | 2 (values used) | `mstfixedselection` |
| Source | `miscellaneous` (Inquiry / Inquiry Source) | 39 (full master; 3 inactive → `active = 0`) | `mstinquirysource` |
| Category | `miscellaneous` (Inquiry / Category) | 795 (full master; 31 inactive → `active = 0`) | `mstinquirycategory` |
| Status | `status` (module Inquiry) | 5 | `mstfixedselection` values used |
| Type | `miscellaneous` (Inquiry / Action Type) | 3 | `mstfixedselection` values used |
| Action Taken / Purpose | `followupremarks` | 87 | every active `mstactiontaken` + `mstactiontobetaken` name (86) + the values used on migrated actions |
| Checklist Master | `taskchecklistmaster` / `taskchecklistmasterdet` (title, days, sort) | 24 sets / 253 items | `mstcheckset` + `msdcheckset` (item names from `mstchecks`, days from `ndaysdiff`) — sets of offices 2/3/4/6 that are active or used by a migrated document; seeded once by `Shared.EnsureChecklistMasters` for all document modules, matched by title |

---

## 4. Changes made on our side

### 4a. Form / view changes (saksham-gated)

| # | Screen | Change | Kind | File | DB column |
|---|---|---|---|---|---|
| 1 | Inquiry form (CI) | Progress block: **Sales Stage**, **Inquiry By**, **Source**, **Lost Reason**, **Category** dropdowns, each with quick-add | New field ×5 | `inquiry/default.asp` (saksham action / follow-up block) | `inqcs.stage`, `calltype`, `sources`, `lossreason`, `Category` |
| 2 | Inquiry form (CI) | **Type** dropdown (drives which action sections show) | New field | same block | `inqcs.types` |
| 3 | Inquiry form (CI) | **1st Action**: Purpose, Priority, Date, Remarks | New field ×4 | same block | saved as a `followup` row |
| 4 | Inquiry form (CI) | **Action Taken**: Action Taken, Action Taken Date, Person Contacted | New field ×3 | same block | `followup` |
| 5 | Inquiry form (CI) | **Next Action To Be Taken**: Next Action, Action To Be Taken Date, Priority, Party Contact, Sales Person, Action To Be Taken Remarks | New field ×6 | same block | separate `followup` row |
| 6 | Inquiry view (CI) | **Sales Stage · Inquiry By · Source · Loss Reason · Type · Category** | New field ×6 | `inquiry/default.asp` (saksham progress view) | same columns |
| 7 | Inquiry view | **Task Check List** tab shown for CI (as for SO / CQ) | Behaviour | `assets/scripts/inquiry.js` (view fill: `aInquiry[16] == 'CI'`) | `taskchecklist` (`CI`) |
| 8 | Inquiry save | Action / follow-up fields saved to `followup` on save | Backend | `app/inquiry.asp` (saksham follow-up save) | `followup` |
| 9 | Inquiry form (CI / CQ / SO / AMC quotation, add / edit) | Bill To label **Customer Billing Branch** (static `#custbranch` label and its JS setters), same label as the Sales Invoice form | Label | `inquiry/default.asp`, `assets/scripts/inquiry.js` | `inqcs.csbranch` |
| 10 | Sales Order form | Ship To label **Customer Shipping Branch** | Label | `inquiry/default.asp` | `inqcs.shippingbranch` |
| 11 | Inquiry view (CI / CQ / SO / AMC quotation) | New row **Customer Billing Branch**: party - place - address of the Bill To branch | New field | `inquiry/default.asp` (`inquiry_view_saks_billto`), `assets/scripts/inquiry.js` | `inqcs.csbranch` |
| 12 | Sales Order view | **Customer Shipping Branch** row shows party - place - address of the Ship To branch | Label + Behaviour | `inquiry/default.asp` (`inquiry_view_so_shipbranch`), `assets/scripts/inquiry.js` | `inqcs.shippingbranch` |
| 13 | Backend | the saksham inquiry array carries 24 trailing values, including Bill To party / place / address and Ship To party / address; `inquiry.js` reads them with `var S = aInquiry.length - 24` (view fill and edit fill) | Backend | `app/inquiry.asp`, `assets/scripts/inquiry.js` | — |

**New fields on the form: 19** (6 progress / type + 13 action) · **on the view: 7** (6 progress / type + Customer Billing Branch) · labels: 2 (Customer Billing Branch, Customer Shipping Branch) · tabs opened: 1 (Task Check List for CI) · behaviour / backend: 3.

### 4b. Database changes (ALTER)

| Statement | Why |
|---|---|
| `ALTER TABLE inqcs ADD stage int NULL` (if missing) | Sales Stage has no column on `inqcs` |
| `ALTER TABLE followup ADD priority int NULL` (if missing) | action Priority |

---

## 5. Not migrated

| Client UI | Client DB | Rows | Why |
|---|---|---|---|
| Items with no product | `trdinqry1prods.nitem` empty | 3 | nothing to link |
| Items whose product was not migrated | `trdinqry1prods` | 8 | product not in module 2 |
| Order No. / Order Value, Quote Value | `norderno`, `nordwonvalue`, `nquotedvalue` | — | derived from the linked documents |
| Margins, approval | `nordermargin`, `nmargin`, `bapproval`, `approvedby`, `approvedon` | — | not on the screen |
| Contact Person of another party (header) | `trhinqry.npartycontact` whose `msdparty.nparty` ≠ the inquiry party | 3 (of 7 such rows) | the person is not a person of the inquiry customer |
| Person Contacted (actions) | `trdinqry2actions.npartycontact1` | 74 (59 persons deleted in eBizWiz + 15 of another party) | person not found for the inquiry customer |

---

## 6. Verification (latest run — 0 errors)

| Check | Client | Ours |
|---|---|---|
| Inquiries | 193,337 | **193,337** `inqcs` CI (193,338 incl. template) |
| Customer | 193,337 with a party | **193,337** resolved, 0 unresolved |
| Items | 332,970 lines (332,967 with a product) | **332,959** `inqcsdet` (8 product not migrated) |
| Actions → follow-ups | 521,948 (0 without their inquiry) | **521,948** `followup` INQ, 0 skipped |
| Type | SALES LEAD 99,873 · DSR - NO ACTION NEEDED 49,271 · DSR - ACTION NEEDED 43,912 · empty 281 | `inqcs.types` |
| Quotation link | 72,985 inquiries have a quotation | module 4 links 110,379 quotations back to their inquiry |
| Check list | 31,819 items on 2,867 inquiries, 20,587 done | **31,819** `taskchecklist` CI on **2,867** inquiries, **20,587** done |
| Checklist Master | check sets of offices 2/3/4/6, active or used | **24** sets / **253** items in `taskchecklistmaster` / `taskchecklistmasterdet` |
| Competitors | 9 `trdinqry3competitor` rows on 6 inquiries | **9** `Competitor:` entries in the remark of **6** inquiries |
| Remark names | Probability on 193,334 (3 blank), Inquiry Value other than NONE on 57,770, Campaign on 6 | **193,334** / **57,770** / **6** remarks carry the name; 0 remarks left with a code |

---

## 7. Notes

- **Active flag of the Inquiry masters.** The client screens (Inquiry Category, Inquiry Source, Lost Reason, Sales Stage) list active rows only, and our dropdowns read `miscellaneous.active = 1`. `SyncInquiryMasterActive` sets `active = 0` on the values that are inactive in eBizWiz, so the dropdowns match the client; documents that carry such a value (e.g. 633 inquiries with an inactive category) still show it on the view.

- **Remark names:** eBizWiz stores Probability, Inquiry Value and Campaign as master codes. The exe writes the names shown on the client screen (`25% in Our Favor`, `Upto Rs. 5 Lacs`, `CA5 - <campaign name>`) — our inquiry has no columns for them.
- **Check list:** the items go to the standard Task Check List component with tag `CI`, exactly like Sales Order (`SO`) and Quotation (`CQ`); the tab is shown for saksham on CI, SO and CQ. The check-set name is not written into any remark: the eBizWiz check sets are the Checklist Master (`taskchecklistmaster` / `taskchecklistmasterdet`), from which a set can be applied on a document.
- **Competitors:** our database has no inquiry competitor table (`QtnCompetitors` / `QuoteCompetitor` exist in the code only for Akpowertech and are not in `SakshamRMtP15329`), so each competitor row is written as a labelled `Competitor: …` line in `inqcs.remark` of its inquiry.
- **Action model** (agreed with the client and senior): Purpose + Action Taken are one follow-up; the Next Action is its own follow-up; Person Contacted is the contact person's code; the Sales Person is the follow-up owner.
- **Type** decides which action sections show on our form (No Action / Action Needed / Sales Lead), as on the client screen.
- **followType** is not a client field; it is derived from the action text so the Follow Up list shows an icon/type.
- **Tax:** inquiry items carry only product and quantity — no rate, discount, tax or charges. GST starts at the Quotation (04).
- Quote No. / Quote Value on the client screen come from the linked quotation; in our app the quotation points back with `inqcs.inqlink`.
- **`approvalstatus`** is written as `''`, the value the app itself posts on every save (`assets/scripts/inquiry.js` `txtapprovalstatus ""`). The column default is `'Pending'`, which would make every migrated inquiry look like it is waiting for approval.
- **Bill To / Ship To labels:** for saksham the inquiry form and view use the Sales Invoice labels — **Customer Billing Branch** (Bill To, `inqcs.csbranch`) and **Customer Shipping Branch** (Sales Order Ship To, `inqcs.shippingbranch`) — on CI, CQ, SO and AMC quotation.
- **Cold Call / Tour Plan:** the eBizWiz screen `Inquiry/DailyCallingList.aspx` picks parties by City / Route (Area) / Profile (step 1) and creates **Inquiry Entry** records with Inquiry By, Category, Sales Stage, Source, Sales Person, Priority, Action To Be Taken and Date (step 2). It has no table of its own — what it creates is in `trhinqry` and is migrated by this module.
