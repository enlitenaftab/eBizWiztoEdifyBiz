# Saksham — eBizWiz to EdifyBiz Migration Report

**Source:** eBizWiz database `saksham70v1_1` (copy of 08/06/2026) · **Target:** EdifyBiz database `SakshamRMtP15329`
**Scope:** offices HO Mumbai, Bangalore, Delhi, Kolkata (eBizWiz offices 2, 3, 4, 6). Offices 1 and 5 are WinMax test offices and are not migrated.
**How to read:** each client menu item shows what the client screen holds, where it sits in the eBizWiz database, where it is in EdifyBiz (screen and table), and what was not migrated and why. Counts are from the latest migration run.


## Summary

Latest migration run: 17 steps, **0 errors**. Every client menu item below was checked against both databases.

| Area | eBizWiz | EdifyBiz |
|---|---|---|
| Parties / contact persons | 20,757 / 83,941 | 20,756 contacts / 83,629 persons |
| Items | 29,966 | 29,966 products |
| Inquiries / follow-ups | 193,337 / 521,948 | 193,337 / 521,948 |
| Quotations | 110,493 (value ₹37,92,99,71,142.73) | 110,493 (same value) |
| Order Received / Order Placed | 7,658 / 6,031 | 7,658 Sales Orders / 6,031 Purchase Orders (same line values) |
| Warranty + Non Warranty Sales | 5,707 (value ₹1,54,78,55,451.71) | 5,707 Sales Invoices (same value) |
| Stock | 7,150 in / 1,326 out, balance per item + office | GRN / MRO / GTA; stock = eBizWiz on 4,352 / 4,352 item + branch |
| AMC quotations / contracts / bills | 8,758 / 4,528 / 7,618 (4,000 billed, ₹11,70,73,427.85) | 8,758 / 4,528 / 7,618 billing cycles, 3,996 bill invoices (same total) |
| Warranty | 6,937 serials, 5,131 PM visits | 4,261 warranty contracts, 6,936 serials, 5,131 PMS |
| Service calls / call bills | 36,164 / ₹2,41,96,132.26 | 36,164 complaints / 960 invoices (same total) |
| Receipts | ₹18,10,20,531.23 | 2,880 payments, same amount, each allocated to its invoice |

**Open points before go-live**
1. **Users:** all migrated users have the Admin role and one temporary password. The client administrator must set each user's rights and every user must set a password.
2. **Branch GSTIN (answered by the client, 24/09/2026):** Saksham has its own GSTIN per branch - Mumbai 27AAFCS0756B1ZM, Delhi 07AAFCS0756B1ZO, Bangalore 29AAFCS0756B1ZI (Kolkata not given yet). The migrated documents keep the eBizWiz behaviour, where every office billed on the Maharashtra GSTIN (a Bangalore office invoice charged IGST to a Karnataka customer), so the migration is left as it is. Setting the branch GSTIN, the current branch addresses and the bank accounts belongs to the go-live setup of the new system, not to the migration of the old data.
3. **Purchase Order charges** are kept as text in the PO Remarks and are not part of the EdifyBiz PO total.
4. **Target remarks** (target number, principal split) are stored but the Target screen has no Remark field.
5. **Documents (Upload Doc / DMS)** of every screen are not part of this migration; the files stay on the client server.

---

## Master > Location

| Client screen | Client fields | Client DB (rows) | EdifyBiz screen | EdifyBiz DB | Result |
|---|---|---|---|---|---|
| Currency | Major Denomination, Short Name, Minor Denomination, Short Name, Active | `mstcurrency` (15: 10 active, 5 inactive) | Master > Currency (Currency, Symbol) | `currency` (name, sname) | 13 migrated: the 10 active + 3 inactive still used by eBizWiz quotations (Rupees, New C, Money). Matched to an existing EdifyBiz currency by name / short name, otherwise created |
| Exchange Rate | Trn. No., Trn. Date, Ref. No., Ref. Date, Remarks; details: Currency, Currency Value, Exchange Currency, Exchange Rate, Effective From | `mstexchangerate` (39) + `msdexchangerate` (517) | Master > Currency Conversion (Symbol, Conversion Rate) | `currencyconversion` (name, rate) | Latest rate to INR of each currency: USD 94, EUR 110, GBP 127, CHF 120, CNY 13, HKD 11, JPY 0.59 |
| Country | Name, Short Name, Currency, Active | `mstcountry` (23) | Master > Country (Country) | `country` (name) | Every country that has a migrated city is present (matched by name, created if missing) |
| State | Name, Country, Active | `mststate` (50) | Master > State (State, State GST Code, Country, Zone/Region) | `state` (name, countrycode, stategstcode) | Every state that has a migrated city is present; GST state codes filled for Indian states |
| City | Name, State, STD Code, Active | `mstcity` (751) | Master > City (City, State, Country, Type) | `city` (name, statecode, countrycode) | **751 / 751** present (matched by name, created with state and country if missing) |
| Route-Area (Office-Wise) | Name, City, Reminder Message, Remarks, Active | `mstroute` (849: HO 276, Bangalore 179, Delhi 222, Kolkata 149) | Contact > address > **Area** | `mltaddress.area` (text) | EdifyBiz has no route master; each party's route name is written as the Area of its address (20,756 parties) |
| Zone | Name, Remarks, Active | `mstzones` (5) + state-zone link `msdstatezone` (0) | — | — | Not migrated: no state or party is linked to a zone in eBizWiz, so there is nothing to attach |

**Not migrated (Location)**

| Client data | Rows | Why |
|---|---|---|
| Currency: Minor Denomination / Minor Short Name | 15 | EdifyBiz currency has only name and symbol |
| Currency: Euro1, Test Rupees | 2 | inactive in eBizWiz and used by no document |
| Exchange Rate history (older dated rates, rates between two foreign currencies) | 510 of 517 detail rows (all but the 7 latest INR rates) | EdifyBiz keeps one current conversion rate per currency (to INR) |
| Country: Short Name, Currency | 23 | EdifyBiz country has only a name |
| Countries India Test, Test27032017 (test), Korea | 3 | no city belongs to them |
| States Daman, Ohio, Malaysia | 3 | no city belongs to them |
| States Dhaka, Andaman | 2 | their only city already existed in EdifyBiz under its own state |
| City: STD Code | 20 filled | EdifyBiz city has no STD code field |
| Route: City, Reminder Message, Remarks (master fields) | 849 | no route master in EdifyBiz; only the route name travels with each party address |
| Zone | 5 | not linked to anything in eBizWiz |

**Notes (Location)**
- The live eBizWiz server shows 753 cities; the database copy used for the migration has 751 (2 cities were added on the live server after the copy).
- The Currency screen lists only active currencies (10); the 3 inactive currencies are kept only because old quotations use them.

---

## Master > Party

| Client screen | Client fields | Client DB (rows) | EdifyBiz screen | EdifyBiz DB | Result |
|---|---|---|---|---|---|
| Party Types | Name, Common, Active | `mstpartytype` (10, all active) | Contact form > **Party Type** dropdown (with + quick-add) | `miscellaneous` (module Contact, type Party Type) | All 10 types are in the Party Type master. Each party's type is set on its contact |
| Party Profile | Name, Email Instance, Active | `mstpartyprofile` (40, all active) | Contact form > **Party Profile** dropdown (with + quick-add); view > Party Profile | `miscellaneous` (module Contact, type Party Profile) | All 40 profiles are in the Party Profile master. Each party's profile is set on its contact |
| Party (Office-Wise) — Party Master | Name, Title, Party Type, Party Profile, Sales Person, Service Person, Tax1, Tax2, Tax3, Reminder Message, Remarks, Active | `mstparty` (20,757 in HO, Bangalore, Delhi, Kolkata) | Contact (form + view) | `contact` | **20,756** contacts. Title is placed before the name (e.g. "M/s. …"). Office goes to the contact's branch. Inactive parties are inactive contacts (6,075) |
| | City, Route (Area), Address, Postal Code, Telephone 1, Telephone 2, Fax, Email, Website | `mstparty` (install address) | Contact > Address (City, Area, Address, Pincode, Landline, Fax, Email, Website) | `mltaddress` (default / ship-to row) | 20,756 addresses. City matched to the city master; route name as Area |
| | Billing Address, Postal Code, Telephone 1/2, Fax, Email | `mstparty` (billing address) | Contact > Additional Addresses | `mltaddress` (bill-to row) | Written when the billing address is different from the install address |
| | Sales Person, Service Person | `mstparty.nusersales` (1,884), `nuserservice` (4) | Contact form + view > Sales Person, Service Person | `contact.crmagent`, `contact.fieldagent` | 1,884 / 4. Every person is matched to an EdifyBiz user |
| | Tax1, Tax2, Tax3 | `mstparty.vtax1/2/3` (139 / 54 / 3) | Contact view > **Licenses & Statutories** tab + Remark | `mltaddress.gstno` / `impexpCode` / `pan` / `vat` of the default address, plus `contact.pan`, `gst_no`, `cst` and `contact.remark` | The client stores these as labelled free text ("GST No.: 27AAZFR4677D1ZH"). The number is pulled out into the standard GST / Import-Export / PAN / VAT fields, and the original text stays in Remarks |
| | Reminder Message | `mstparty.vremindermessage` (446) | Contact view > Remark | `contact.custom1` + `contact.remark` | Stored, and also written into Remarks as "Reminder Message: …" so the user can see it |
| Party (Office-Wise) — Party Detail (contact persons) | Contact Person, Title, Designation, Telephone 1/2, Mobile, Email, Date of Birth, Anniversary, Spouse Name + DOB, Child 1 / Child 2 DOB, Communication Model, Remarks | `msdparty` (83,941) | Contact > Persons (Name, Title, Designation, Landline, Mobile, Email, Birthday, Anniversary, Spouse's Birthday, 1st / 2nd Child's Birthday, Communication Model, Remark) | `mltcontact` | **83,629** persons. Designations and communication models added to their masters |
| Party (Office-Wise) — Party Info tab | Tech. Category, Character / Numeric / Logical / Date Input, Input Selection | `msdpartyinfo` (0) | — | — | Nothing to migrate: the tab is empty in eBizWiz |
| Change Party Details | Filter by City / Route / Profile / Party and change them in bulk | — (utility, changes `mstparty`) | Contact edit | — | Utility screen, no data of its own. Its result is already in the migrated parties |
| Bank | Name, Branch, Address, Postal Code, Telephone 1/2, Fax, Email, Contact Person, Designation, CP Email / Telephone / Mobile, Our Bank, Account Since, Account Type, Account Number, Active | `mstbanks` (2: BOB, SBI) | Company > **Banks** tab (Bank, Branch, Account Number, Account Type, default) | `companybank` | 2 / 2. BOB (Malad, Saving, 1234567890, our bank → default) and SBI (Malad). Used as Bank/Cash on migrated receipts |
| Approval | Approve new / edited parties | — (setting `defparty.bapproval`) | — | — | Approval is switched off in every eBizWiz office and there is no pending approval data |

**Not migrated (Party)**

| Client data | Rows | Why |
|---|---|---|
| Party with no name | 1 (Delhi, inactive, no persons) | there is no name to create the contact with |
| Contact persons with no party | 311 | not linked to any party in eBizWiz |
| Contact person of a test-office party | 1 | its party belongs to office 1 (WinMax test office) |
| Party Type: Common flag | 2 types (PRINCIPAL, test party) | not needed: in Saksham EdifyBiz every contact is visible to every branch |
| Party Profile: Email Instance flag | 40 | EdifyBiz Party Profile has no such flag |
| Contact person: Child 1 / Child 2 Name | 9 persons | EdifyBiz person has child birthdays but no child-name field |
| Contact person: Active | 72 inactive persons | EdifyBiz person has no active flag. The persons are kept |
| Contact person: TPC | 43 | not migrated, as decided earlier in the project |
| Bank: Address, Postal Code, Telephones, Fax, Email, Contact Person details, Account Since | 2 banks | EdifyBiz company bank has no such fields |
| Party form defaults per office | 7 (`defparty`) | eBizWiz form settings (default type / profile / city, list of allowed persons), not party data |

**Notes (Party)**
- The client's HO party list shows 5,840 parties. The HO figures in the migrated copy are 8,997 parties in total, of which 5,719 are active. The screenshot is from the live server, which has parties added after the copy date.
- The client's Party Types screen shows 11 types. **"sun pharma"** is not in the copy used for the migration, so it was created on the live server after the copy date.
- Tax1, Tax2, Tax3 are shown in the contact's **Licenses & Statutories** tab (GST, PAN, Import-Export, VAT) and the original text also stays in the **Remark**. Reminder Message is visible in the **Remark**.

---

## Master > Item

| Client screen | Client fields | Client DB (rows) | EdifyBiz screen | EdifyBiz DB | Result |
|---|---|---|---|---|---|
| Item Type | Name, Warranty Item, Labour Item, Remarks, Active | `mstitemtype` (19, all active) | Master > **Labels** (Product labels); Product form + view > Labels | `label` (category PRO) + `labelrelation` | All 19 item types become Product labels. Each product carries its item type as a label (12 types are used by items). An item whose type is a Labour type becomes a **service** product (28 items) |
| Item Category | Name, Remarks, Active | `mstitemcategory` (1,125) | Master > **Product Category**; Product form > Product Category | `prodcat` | 1,125 / 1,125. Each product gets its category |
| Units | Name, Active | `mstunits` (6) | Master > **Unit**; Product > UOM | `unit` | 6 / 6 (matched to an existing EdifyBiz unit by name, otherwise created). Used by items: Numbers 28,131, Number 36, Per Day 8, Set 3 |
| Items [Models and Spares] | Item Code, Name, Additional Description, Item Type, Item Category, Currency, Units, Party, Purchase Price, Selling Price, Class, Remarks, Active | `mstitems` (29,966: HO 29,909, Bangalore 26, Delhi 28, Kolkata 3) | Product (form + view): CAS# / SKU# / Part#, Name, Profile, Labels, Product Category, Currency, UOM, Principal Name, Purchase Price, Product Price, Class, Remark | `product` | **29,966** products. Inactive items are inactive products (3). Each product also gets the standard Default Brand / model / **Default Batch**, as the Product form creates |
| | Warranty Months, Maintenance Visit, Contract Amount 1–4 | `mstitems` (9,314 / 4,894 / 0 filled) | Product > Remark | `product.remark` | Written into Remarks as "Warranty(months): …", "Maintenance Visits: …" (our product has no such fields) |
| | Item suppliers | `msditemsuppliers` (7,734) | Product > Supplier tab | `contprod` (type S) | 1,232 supplier links (the item's own principal and suppliers from test offices are not repeated) |
| Item Opening Balance [Office-wise] | Office, Item, Good / Defective opening qty, date | `mststkdt` (2,590 office + item rows with an opening: HO 879, Bangalore 787, Delhi 923, Kolkata 1) | Inventory > **Opening Stock**; Stock Summary | `stockjv` (JV "Opening Stock") + `stocktrans` | Good 337,749 + damaged 100 posted at each branch on the client's opening date, exactly as EdifyBiz's Opening Stock screen posts it |
| Change Item Levels [Office-wise] | Min / Max / Reorder level, Min / Max order qty per office | `mststkdt` (0 filled) | — | — | Nothing to migrate: no office has a level filled |
| Change Item Details [HO] | Filter by Category / Type / Name / Code and change them in bulk | — (utility, changes `mstitems`) | Product edit | — | Utility screen with no data of its own. Its result is already in the migrated items |
| Approval | Approve new / edited items | — | — | — | No approval data exists in eBizWiz |

**Not migrated (Item)**

| Client data | Rows | Why |
|---|---|---|
| Item Type: Warranty Item flag | 3 types | EdifyBiz Product label has no such flag. Warranty per machine comes with the warranty / AMC contracts |
| Item Category: Remarks | 1 | EdifyBiz Product Category has no remark |
| Item: Technical Set | 1 item | no matching field on EdifyBiz product |
| Item: Minimum Order Qty | 1,712 items, all = 1 | default value; no such field on EdifyBiz product |
| Office-wise Bin No. | 41 | no bin / rack field in EdifyBiz |
| Purchase / selling price change log | 198 | history only; the current prices are on each product |
| Item opening balance of a test-office item | 1 row | the item belongs to office 1 (WinMax test office) |
| Item documents (More > Upload Doc.) | — | document management (DMS) is out of scope |

**Notes (Item)**
- Client item codes are unique, but 1,732 item names repeat across 8,423 items (e.g. "L15/11/B510 Tmax 1100 with Flap Door" exists under 20 item codes). Every document line (quotation, order, invoice, stock, contract, call) is therefore linked to its product by **Item Code**, not by name.
- The client's Item Type and Item Category lists show 19 and 1,153 rows. 1,153 is from the live server; the migrated copy has 1,125 categories (28 were added after the copy date). The HO item list shows 30,521 on the live server against 29,909 in the copy.
- Opening stock was dated 01/01/1900 in eBizWiz for some rows. Those rows use the date the balance was entered instead.
- **Closing stock = the client's own stock.** EdifyBiz stock = opening + stock in − stock out − sold − call spares, plus one "eBizWiz balance adjustment" per item where the client had changed the balance directly in eBizWiz (Change Stock Details, which leaves no transaction). About 2,500 items needed it (e.g. "Cryo Box 2 Inch" at HO: 1,581 received, nothing issued or sold, client balance 0). The adjustment is a standard EdifyBiz stock entry dated when the client last changed that balance, so every branch shows the same stock as eBizWiz and the full transaction history is kept. Verified: 4,352 / 4,352 item + branch stock balances equal eBizWiz (good 341,766, damaged 663; the 118 negative balances of eBizWiz, −546 units, are the same). Items of a Labour / service type hold no stock in EdifyBiz (17 item + branch balances, 310 units in eBizWiz).

---

## Master > Calls

| Client screen | Client fields | Client DB (rows) | EdifyBiz screen | EdifyBiz DB | Result |
|---|---|---|---|---|---|
| Complaint | Name, Email Instance, Active | `mstcomplaint` (100, all active) | AMC > Master > **Complaint Type**; Complaint form + view > Complaint | `complainttype` | 100 / 100. The call's "Our Complaint" is set on 28,681 / 28,681 calls |
| Defect | Name, Email Instance, Active | `mstdefect` (49) | Complaint > Call Visit Logs > Remarks | `callvisits.remark` | EdifyBiz has no Defect master. Each fault row of a call visit is written to that visit's remark as "Complaint: … \| Defect: … \| Repair: …" (32,522 fault rows on 32,352 visits, all present) |
| Repair | Name, Email Instance, Active | `mstrepair` (45) | Complaint > Call Visit Logs > Remarks | `callvisits.remark` | As Defect (same fault row) |
| Call Pending Reason | Name, Email Instance, Active | `mstcallpendingreasons` (129, 128 active) | Complaint view > Remark | `contractcall.remark` | EdifyBiz has no call pending-reason master. The reason is written into the call remark as "Pending Reason: …" on 3,136 / 3,136 calls |
| Call Cancel Reason | Name, Email Instance, Active | `mstcallcancelreasons` (26) | Complaint > Status **Cancelled** + Remark | `contractcall.status` + `remark` | 4,767 / 4,767 cancelled calls get status Cancelled with "Cancel Reason: …" in the remark |
| Escalation | Designation, User Name, Escalation Time, Remarks, Active | `mstescalation` (2) | — | — | Not migrated: both rows belong to office 1 (WinMax test office) |
| Feedback Question | Question, Display Order, Question Type, Active | `mstquestion` (15) | Complaint view > **Customer Feedback Remark** | `customerfeedback.remarks` | EdifyBiz has no feedback-question master (its Questions master is only for HR Induction / Resignation). Each answered question is one line "Q: … - A: …" in the call's feedback (3,420 calls with feedback) |
| Feedback Answer | Answer, For Call Visit Page, Active | `mstanswer` (12) | Complaint view > Customer Feedback Remark; **Customer Rating** | `customerfeedback.remarks`, `customerfeedback.rating` | Answer text is kept in each line. The CSI answer bands (Below 38%, 39–66%, 67–84%, 85–100%) set the 1–5 star Customer Rating |

**Not migrated (Calls)**

| Client data | Rows | Why |
|---|---|---|
| Email Instance flag (Complaint / Defect / Repair / Pending / Cancel) | set on Complaint 6, Defect 1, Repair 1, Pending Reason 3, Cancel Reason 1 | EdifyBiz Complaint Type has no such flag; no e-mail rule is attached to these values |
| Defect, Repair, Pending Reason, Cancel Reason as separate masters | 49 / 45 / 129 / 26 | EdifyBiz has no such masters (AMC Master holds only Complaint Type, Email Template, Plan). The values used on calls are kept as text on each call / visit, so nothing used is lost |
| Escalation | 2 | test office 1 only |
| Feedback Question: Display Order, Question Type; Answer: For Call Visit Page | 15 / 12 | no feedback-question master in EdifyBiz |

**Notes (Calls)**
- The client's Complaint list shows 100, Defect 49, Repair 45. These equal the migrated copy.
- The Complaint (Our Complaint) value is a real dropdown in EdifyBiz (Complaint Type). Defect / Repair / reasons are readable on each call and visit but cannot be picked from a list for new calls.

---

## Master > Inquiry

| Client screen | Client fields | Client DB (rows) | EdifyBiz screen | EdifyBiz DB | Result |
|---|---|---|---|---|---|
| Inquiry Category | Name, Email Instance, Active | `mstinquirycategory` (795: 764 active) | Inquiry / Quotation / Sales Order form + view > **Category** (dropdown with + quick-add) | `miscellaneous` (Inquiry / Category) | All 795; the 31 inactive ones are inactive in EdifyBiz too, so the dropdown shows the same 764 as the client. Set on 193,335 / 193,335 inquiries |
| Inquiry Source | Name, Email Instance, Active | `mstinquirysource` (39: 36 active) | Inquiry form + view > **Source** | `miscellaneous` (Inquiry / Inquiry Source) | All 39 (3 inactive). Set on 192,298 / 192,298 inquiries |
| Action To Be Taken | Name, Email Instance, Active | `mstactiontobetaken` (44, all active) | Inquiry > Follow Up > **Purpose** | `followupremarks`; `followup.purpose` | All active values are in the list. Set on 521,921 / 521,921 follow-ups |
| Action Taken | Name, Email Instance, Active | `mstactiontaken` (44: 43 active) | Inquiry > Follow Up > **Action Taken** | `followupremarks`; `followup.remarkscode` | All active values are in the list. Set on 446,400 / 446,400 follow-ups (when no action was taken, the purpose is shown, so 521,944 follow-ups carry a value) |
| Lost Reason | Name, Email Instance, Active | `mstorderlostreason` (60: 54 active) | Inquiry / Quotation form > **Lost Reason**, view > **Loss Reason** | `miscellaneous` (Inquiry / Loss Reason) | All 60 (6 inactive). Set on 109,233 / 109,233 inquiries |
| Sales Stage | Name, Email Instance, Active | `mstsalesstage` (13: 9 active) | Inquiry form + view > **Sales Stage** | `miscellaneous` (Inquiry / Sales Stage) + `inqcs.stage` | All 13 (4 inactive: 2. Qualifying, 5. Warm Confirmed, 7. Hot Confirmed, 9. Super Hot Confirmed). Set on 193,336 / 193,336 inquiries |

**Not migrated (Inquiry)**

| Client data | Rows | Why |
|---|---|---|
| Email Instance flag | set on Category 6, Lost Reason 6, Sales Stage 5 | EdifyBiz has no such flag on these lists |

**Notes (Inquiry)**
- The client screens list active values only. EdifyBiz keeps the inactive ones too, but switched off, so the dropdowns show the same values as the client. Old documents that carry an inactive value (e.g. 633 inquiries with an inactive category) still show it on their view. If such a document is edited, the user must pick an active value, as in eBizWiz.
- The client's Inquiry Category list shows 769 on the live server against 764 active in the copy (5 added after the copy date).
- "Action To Be Taken" and "Action Taken" are one list in EdifyBiz (follow-up remarks). It feeds both the Purpose and the Action Taken dropdown of a follow-up.

---

## Master > Technical

| Client screen | Client fields | Client DB (rows) | EdifyBiz screen | EdifyBiz DB | Result |
|---|---|---|---|---|---|
| Technical Category | Name, Input Data Type, From / To Range, Unique, Party Information, Office Information, Print, Units, Remarks, Active | `msttechnicalcategory` (11) | — | — | Not migrated: all 11 were created in office 1 (WinMax test office) and are test / dummy entries (Test, test102, DUMMY TECHNICAL CATE …) |
| Technical Records | Name, Technical Category, Active | `msdtechnicalrecords` (3) | — | — | Not migrated: 3 test records of office 1 (MJ Test, Test Item name27042017, test111) |
| Technical Set | Name, Remarks, Email Instance, Active + detail (categories in the set) | `msttechnicalset` (2) + `msdtechnicalset` (0) | — | — | Not migrated: 2 sets of office 1 (Chamber Dimensions …, test), no categories inside |

**Where technical data is used in eBizWiz (offices HO, Bangalore, Delhi, Kolkata)**

| Client data | Rows | Content | Result |
|---|---|---|---|
| Party > Party Info tab (`msdpartyinfo`) | 0 | — | nothing to migrate |
| Office info (`msdofficeinfo`) | 0 | — | nothing to migrate |
| Quotation item technical grid (`trdquote4tech`) | 3 (Delhi, 2012–2013) | category "Chamber Dimensions Ø x depth in mm" with **no value entered** | nothing to migrate |
| Order Received technical grid (`trdordrc7tech`) | 2 (Delhi, 2013) | same category, **no value entered** | nothing to migrate |
| Sales / Contract / AMC offer / Repair estimate technical grids | 0 | — | nothing to migrate |
| Item's Technical Set (`mstitems.ntechnicalset`) | 1 item | set "test" | listed under Master > Item (not migrated) |

**Notes (Technical)**
- The technical masters were never used with real data in the four live offices, so EdifyBiz needs no technical master for Saksham.

---

## Master > Terms

| Client screen | Client fields | Client DB (rows) | EdifyBiz screen | EdifyBiz DB | Result |
|---|---|---|---|---|---|
| Payment Term | Name, Email Instance, Active | `mstpaymentterms` (73: 71 active) | Terms / Term Details of each document | `inqcs.terms` (Quotation, Sales Order), `pur_order.term`, `sal_order.term` | EdifyBiz has no payment-term master; documents carry free-text Terms. Each document's payment-term grid (term, %, amount) is written into its Terms as "Payment Terms: …". Quotation 35,001 / 35,001, Order Received 4,023 / 4,023, Order Placed 1,730 / 1,730, Sales Invoice 1,307 / 1,307. The GRN's single payment-term row goes to its remark |
| Contract Payment Schedule | Name, Months, Email Instance, Active | `mstpaymentschedule` (7, all active: Yearly 12, Half Yearly 6, Once In 4 Months 4, Quarterly 3, Monthly 1, Advance 12, Contract Payment 12) | AMC Quotation > Term Details; AMC Contract > **Payment Terms** | `inqcs.terms` (AMC quotation), `amc.paymentterms` | The schedule name + "Beginning / End of Period" is on 8,758 / 8,758 AMC quotations and 4,528 / 4,528 AMC contracts. The actual bill dates and amounts of each contract are migrated as its **Billing Cycle** (`billingcycle`, 7,618 rows) |
| Terms and Conditions Set | Name, Email Instance, Term and Cond. (text), Active | `msttermset` (2) | Terms / Term Details (Remark on AMC contracts) | `inqcs.terms`, `pur_order.term`, `sal_order.term`, `amc.remarks` | Only set 1 "ESCO Standard Terms and Conditions" is used: Quotation 3,916, AMC Quotation 1,436, AMC Contract 741, Order Placed 405, Sales Invoice 49. All present as "Terms & Cond.: ESCO Standard Terms and Conditions" |

**Not migrated (Terms)**

| Client data | Rows | Why |
|---|---|---|
| Payment Term / Payment Schedule / T&C Set as separate masters | 73 / 7 / 2 | EdifyBiz documents have free-text Terms, no master list to pick from; every value used on a document is on that document |
| T&C Set body text | 1 set | holds only the placeholder "TERMS AND CONDITION..." (22 characters), no real terms |
| T&C Set "SAKSHAM SALES BANK DETAILS" | 1 | empty and never used on a document |
| Email Instance flag | Payment Term 1, T&C Set 1 | no such flag in EdifyBiz |

**Notes (Terms)**
- The client's Payment Term list shows the 71 active terms. The 2 inactive ones (DUMMY PAYMENT, ADVANCE) were never used on a document.

---

## Master > Taxes

| Client screen | Client fields | Client DB (rows) | EdifyBiz screen | EdifyBiz DB | Result |
|---|---|---|---|---|---|
| Post Tax Charges | Name, Active | `mstprepostchgs` (218: 216 active) | Adjustments / GST of each document | see the table below | EdifyBiz has no charge master for Saksham: the Adjustment name is typed on each document (the Adjustment dropdown master is used only by other companies). Each charge a document used (207 different charges) is on that document with the client's name, % and amount |
| Taxes | Name, Active + detail (tax components, View Detail) | `msttaxes` (27) + `msdtaxes` (85) | GST on each invoice line (CGST / SGST / IGST) | `tax` (CGST, SGST, IGST) + `sal_tax` | EdifyBiz GST uses its fixed tax master (CGST / SGST / IGST) and a GST % per line. Client taxes are rebuilt as GST on each invoice line; old taxes (VAT, CST, Service Tax) are kept as the charge text on the document |
| Tax Set | Name, Remarks, Email Instance, Active + detail | `msttaxset` (21: 15 active) + `msdtaxset` (25) | Invoice line > GST (%) | `sal_order_det.gst` | Used only where an invoice line has a Tax Set but no GST charge (29 lines): its % becomes the line's GST % |

**Where the charges of each document went**

| Client document | Post-tax charge rows (documents) | EdifyBiz | Result |
|---|---|---|---|
| Quotation | 324,875 (95,323) | Quotation > Adjustments (`inq_adjust`) | charge name, %, amount as printed; Grand Total = client total |
| Order Received | 24,719 (7,059) | Sales Order > Adjustments (`inq_adjust`) | same |
| AMC Quotation | 10,525 (7,486) | AMC Quotation > Adjustments (`inq_adjust`) | same |
| Sales Invoice (Warranty / Non Warranty Sales) | 11,799 (3,193) | Sales Invoice > GST per line (`sal_tax`) + Adjustments (`sal_adjust`) | GST charges become CGST / SGST / IGST on each line; other charges become Adjustments |
| AMC Contract bills | 6,012 (3,858 contracts) | AMC bill Sales Invoice > GST (`sal_tax`) + Adjustments (`sal_adjust`) | charges scaled to each bill; invoice total = eBizWiz bill amount |
| Service call bills | 8 (7) | Service Call Sales Invoice > GST / Adjustments | same rule |
| Order Placed | 2,180 (1,381) | Purchase Order > **Remarks** ("Post Tax Charges: …") | the Saksham PO screen shows no adjustments and a PO carries no tax, so the charges are kept as text |

**Not migrated (Taxes)**

| Client data | Rows | Why |
|---|---|---|
| Post Tax Charges / Taxes / Tax Set as masters | 218 / 27 / 21 | no matching master for Saksham in EdifyBiz (see above); every value used is on its document |
| Unused charges | 11 of 218 | not used on any quotation, order, invoice, AMC quotation or contract |
| Email Instance flag (Tax Set) | — | no such flag in EdifyBiz |

**Notes (Taxes)**
- All Saksham offices invoice on the Maharashtra GSTIN, so GST on an invoice is CGST + SGST when the customer is in Maharashtra and IGST otherwise, as in EdifyBiz's own invoice.
- Purchase Order charges are text only. PO totals therefore do not include the client's charges.

---

## Master > Checks

| Client screen | Client fields | Client DB (rows) | EdifyBiz screen | EdifyBiz DB | Result |
|---|---|---|---|---|---|
| Check List (check set) | Name, For Call Visit Page, Active + detail (checks in the set, days) | `mstcheckset` (34: HO 27, office 1 7; 30 active = client screen) + `msdcheckset` | **Checklist Master** (applied on a document's Task Check List tab) | `taskchecklistmaster` + `taskchecklistmasterdet` (title, days, sort) | **24 / 24** active HO sets with their **253** items. They are the same 24 HO sets as on the client screen. Every set used by a migrated document is among them |
| Checks | Name, Active | `mstchecks` (251, all active) | Checklist Master items; Task Check List tab of each document | `taskchecklistmasterdet.title`, `taskchecklist.title` | The checks inside the 24 sets are Checklist Master items. The checks ticked on documents are on each document's **Task Check List** (161 different checks) |

**Document check lists (Task Check List tab)**

| Client document | EdifyBiz | Items (documents) |
|---|---|---|
| Inquiry | Customer Inquiry | 31,819 (2,867) |
| Quotation + AMC Quotation | Customer Quotation | 297,533 (26,719) |
| Order Received | Sales Order | 149,662 (4,702) |
| Order Placed | Purchase Order | 82,814 (3,290) |
| Warranty / Non Warranty Sales | Sales Invoice | 56 (56) |
| Service Call | Complaint | 224,583 (22,363) |

Each item keeps its check name, done flag, dates, allotted user and remark, as verified per module.

**Not migrated (Checks)**

| Client data | Rows | Why |
|---|---|---|
| Office 1 check sets (Service Check List, test, Test 2, Testing, Test 04042017, Test 27032017, OR CHECK) | 7 | WinMax test office; no migrated document uses them |
| Inactive HO sets with no checks (CHECK LIST, Order Pending Checklist, Orders Check List (test office)) | 3 | inactive, empty and never used |
| Checks that are in no active set and on no document | 78 | nothing to carry; EdifyBiz has no stand-alone checks list (checks live inside a Checklist Master set) |
| Check List: For Call Visit Page flag | — | EdifyBiz Checklist Master has no such flag |

**Notes (Checks)**
- 24 checks were ticked on documents but belong to no active set; their names are on those documents' Task Check List.
- The check-set name is no longer written into document remarks; the set is available as a Checklist Master and the items are on the Task Check List tab.

---

## Master > Office

| Client screen | Client fields | Client DB (rows) | EdifyBiz screen | EdifyBiz DB | Result |
|---|---|---|---|---|---|
| Office Type | Name, Head Office, Store, Under, Remarks, Active | `mstofficetype` (6, all active) | Company > Address > **Office Type** dropdown | `miscellaneous` (BDS / Office Type) | All 6 types (HEAD OFFICE, BRANCH OFFICE, WAREHOUSE, FACTORY, REGIONAL OFFICE, REPRESENTATIVE OFFICE). Each branch gets its type: HO = HEAD OFFICE, Bangalore / Delhi / Kolkata = BRANCH OFFICE |
| Office | Name, Alias Name, Prefix ID, Office Type, Under, City, Zone, Address, Postal Code, Telephone 1 / 2, Fax, Email, Website, Remarks, Active | `mstoffice` (6: 4 Saksham + 2 WinMax test offices) | Company > **Address** (branch) | `companyaddress` | 4 branches: SAKSHAM - HO MUMBAI, SAKSHAM - BANGALORE, SAKSHAM - DELHI, SAKSHAM - KOLKATA, with the office's address, postal code, telephones, fax, email, website, zone and remarks. Every migrated document, contact and stock row sits on its office's branch |
| Approval | Approve new / edited offices | — | — | — | No approval data exists in eBizWiz |

**Not migrated (Office)**

| Client data | Rows | Why |
|---|---|---|
| WINMAX TEST OFFICE, WINMAX TEST OFFICE FOR DASHBOARD TESTING | 2 | test offices of the software vendor, out of scope |
| Office Type: Head Office / Store flags, Under | 6 | the EdifyBiz Office Type list has only a name |
| Office: Alias Name | 4 | all say "SAKSHAM TECHNOLOGIES PVT. LTD." = the EdifyBiz company name |
| Office: Prefix ID (MRO, BN, DL, KL), Under, Logo | 4 | document numbers are migrated exactly as in eBizWiz; new numbers follow EdifyBiz's own numbering setup |

**Notes (Office)**
- **Branch city is Mumbai for all four branches.** All Saksham offices invoice on the Maharashtra GSTIN, so GST (CGST + SGST vs IGST) is worked out from Maharashtra. **Open with the client:** if Bangalore, Delhi or Kolkata have their own GSTIN, only the branch city in Company > Address has to change.
- In eBizWiz the Kolkata office address is a Mumbai (Malad) address; it is migrated as entered.
- User access to branches comes from the client's user–office rights (see Master > Users and Rights).

---

## Master > Users and Rights

| Client screen | Client fields | Client DB (rows) | EdifyBiz screen | EdifyBiz DB | Result |
|---|---|---|---|---|---|
| Designation [User Type] | Name, Outside Designation, Under, Allowed Discount (%), Allow more discount in User Master, Remarks, Active | `mstdesignation` (7,677, all active) | Contact > Person > **Designation** (search list) | `desigmaster` | All 7,677 designations, plus the free-text designations typed on contact persons |
| Users (user master) | Name, Login ID, Password, Address, Postal Code, Residence Tel, Mobile, Email, Designation, Admin, Office access, Active | `mstusers` (319: 205 active) + office access `msduseroffice` | EdifyBiz **user accounts** | `users` | **309** users (one per distinct name). Every Sales / Service Person, Executive, Assigned To and Created By on migrated data points to the right user. Mobile and postal code are carried. Users inactive in eBizWiz are **suspended** (104; login refused). Branch access comes from the client's user–office access |
| Change User Details | Filter by Designation / Admin / User and change them in bulk | — (utility) | user account edit | — | Utility screen with no data of its own |
| Approval | Approve new / edited users | — | — | — | No approval data exists in eBizWiz |

**Not migrated (Users and Rights)**

| Client data | Rows | Why |
|---|---|---|
| User rights (roles and menus) | `msduserroles` 1,383, `msdrolemenu` 2,672, `mstmenu` 245 | eBizWiz menus and EdifyBiz modules do not match one to one; rights are set in EdifyBiz by the client's administrator |
| User login ID / password | 319 | eBizWiz passwords cannot be carried; new EdifyBiz logins are created (see Notes) |
| User: designation, email, address, residence tel, Admin flag, discount limits | 319 | EdifyBiz user has no such fields (email is set by the user / admin) |
| Designation: Under (hierarchy), Outside Designation, Allowed Discount | 7,677 | EdifyBiz designation list has only a name |
| User ↔ inquiry category / location restrictions | `msduserInqryCategories` 2,870, `msduserlocation` 7 | no such restriction in EdifyBiz |

**Notes (Users and Rights) — action needed before go-live**
- **All migrated users currently have the Admin role and the same temporary password.** The client's administrator must set each user's role (rights) and have every user set their own password before go-live.
- Duplicate user names in eBizWiz (319 records, 309 names) are one user in EdifyBiz; a name is suspended only when none of its eBizWiz records is active.

---

## Master > Expenses

| Client screen | Client fields | Client DB (rows) | EdifyBiz screen | EdifyBiz DB | Result |
|---|---|---|---|---|---|
| Expense Type | Name, Remarks, Active | `mstexpensetype` (3: BY ROAD, Expense, UTD) | Expense > Settings > **Expense Type** tab | `ExpCategory` | 3 / 3 |
| User Expense | Trn. No., Trn. Date, Office, User Name, Remarks; More > Expense Details (type, date, amount, remarks), Upload Doc | `trhexpense` (3) + `trdexpense1detail` (2) | **Expense** (Employee, Date, Currency; Expense Details tab) | `expense` + `expensedetails` | 3 / 3 expenses (HO, Bangalore, Kolkata), 2 / 2 lines = ₹1,250 (travelling ₹1,000 + food allowance ₹250). The header remark is in the first line's Narration |
| Expense Approval | Approve user expenses | `trhexpense.bapproval` | Expense > Status (**Sanctioned**) + Status tab | `expense.status`, `expensestatus` | The 1 approved expense is Sanctioned with the status row "Approved in eBizWiz"; the other 2 are "Not Entered" |

**Not migrated (Expenses)**

| Client data | Rows | Why |
|---|---|---|
| Expense documents (Upload Doc) | — | document management (DMS) is out of scope |

**Notes (Expenses)**
- eBizWiz holds only 3 test-like expense entries (the only one with lines is "expense of 17.05.2017 --- test"); the Expense module was not used in practice.

---

## Marketing & Target

| Client screen | Client fields | Client DB (rows) | EdifyBiz screen | EdifyBiz DB | Result |
|---|---|---|---|---|---|
| Campaign Entry | Trn. No., Trn. Date, Campaign Name, Objective, Expected Sales, From / To Date, Ref. No. / Date, Remarks, Comment; More > Campaign Details (cities, party profiles, items), Promotions and Budget (mode, budget) | `trhcampa` (8) + `trdcampa1geogr` (3), `trdcampa3prods` (4), `trdcampa4pprof` (2), `trdcampa5modes` (4) | **Mass Mail** campaign (Title, Subject, Schedule Date, Message Body) | `mmcampaign` | 8 / 8. Name → Title, Objective → Subject, From Date → Schedule Date, status Sent. Everything else (number, dates, period, expected sales, mode / budget, cities, party profiles, items, ref, remarks, comment, approval) is listed in the Message Body |
| Sales Target on User | Trn. No., Trn. Date, Sales Person, Ref. No. / Date, Remarks; More > Target Period (from, to, amount), principal split | `trhtargt` (89) + `trdtargt1period` (239) + `trdtargt2items` (205) | **Target** (Users × months grid, module Sales With Amount) | `target` (module SAL) | Each yearly period is spread over its 12 months: **2,856** month rows for **87 / 87** users. Total **₹6,41,80,07,001** = client total exactly. Trn. No., ref, remarks and the principal split are kept in the target remark |
| Sales Target on Office | Trn. No., Trn. Date, Office, Ref. No. / Date, Remarks; More > Target Period | `trhtargtoffice` (7 in the 4 offices) + periods + items | — | — | **Not migrated** — see below |

**Not migrated (Marketing & Target)**

| Client data | Rows | Why |
|---|---|---|
| Sales Target on Office | 7 headers (2012–2018) | EdifyBiz targets are per user only (no branch / office target). Putting an office figure on a user would invent an owner and double that user's target |
| Campaign recipients / mail template | — | eBizWiz holds no recipient list or mail body for the campaigns; the Mass Mail entries are a record of the campaigns |
| Campaign expense, Target actions | 0 / 0 | no data |
| Upload Doc | — | document management (DMS) is out of scope |

**Notes (Marketing & Target)**
- Two target periods of one user for the same year (Manish Saini, FY 2017-18: ₹1 Cr + ₹2 Cr) are added into one year, so 239 periods give 238 user-years × 12 months.
- The Target screen has no Remark field. The kept remark (target number, ref, principal split) is stored with each month row but is not shown on the screen.
- Of the 8 campaigns, 3 are clearly tests ("aa", "Test CAmpaign", "test"). Four carry the mode "Mass Mailing" (Esco Laboratory Shakers, Diwali and Deepavali greetings, Bruker Bravo Raman). "Mails : Sales Team" (2021) has no mode.

---

## Inquiry

| Client screen | Client fields | Client DB (rows) | EdifyBiz screen | EdifyBiz DB | Result |
|---|---|---|---|---|---|
| Inquiry Entry / Offer Request Entry — header | Trn. No., Trn. Date, Party, Contact Person, Sales Person, Inquiry By, Category, Inquiry Details, Campaign, Source, Type | `trhinqry` (193,337) | Inquiry > **Customer Inquiry** (form + view): Auto No., Inquiry Date, Customer, Customer Billing Branch, Contact Person, Executive, Inquiry By, Category, Source, Type, Remark | `inqcs` (type CI) | **193,337 / 193,337**. Party 193,337, Sales Person 193,336, Inquiry By 193,336, Category 193,335, Source 192,298, Type 193,056 — all equal to eBizWiz. Contact Person 185,897 of 185,900 (see below). Inquiry Details, Campaign (6) and Comment (4,024) are in the Remark |
| Inquiry Entry — Inquiry Progress Details | Sales Stage, Status, Approx. Close Date, Lost Reason, Check List, Probability %, Inquiry Value, Quote No. / Value, Order No. / Value, Remarks, Comment | `trhinqry` | Customer Inquiry: Sales Stage, Status, Valid Date, Lost / Loss Reason, Task Check List tab, Remark | `inqcs`, `taskchecklist` | Sales Stage 193,336, Status 193,336, Close Date 106,280, Lost Reason 109,233 — all equal. Probability (193,334) and Inquiry Value (57,770) are in the Remark. Quote / Order No. and value are not typed on the inquiry: the migrated quotation points back to its inquiry (72,993 inquiries have a quotation) |
| Inquiry Entry — More > Items | Item, Quantity | `trdinqry1prods` (332,970) | Customer Inquiry > Items | `inqcsdet` | **332,959** lines, each on the right product by Item Code |
| Inquiry Entry — More > Check List | Check, Done, Date, Allotted To, Remarks | `trdinqry4checks` (31,819 on 2,867 inquiries) | Customer Inquiry > **Task Check List** tab | `taskchecklist` (module CI) | **31,819 / 31,819** on 2,867 inquiries |
| Inquiry Entry — More > Competitors | Competitor, Product, Feature, Price, Order Won | `trdinqry3competitor` (9 on 6 inquiries) | Customer Inquiry > Remark | `inqcs.remark` | 6 / 6 inquiries |
| Inquiry Entry — 1st Action / Action Taken / Next Action; Inquiry Follow-up / Action | Type, Purpose, Date, Priority, Purpose Remarks; Action Taken, Action Taken Date, Remarks, Person Contacted; Next Action, Date, Priority, Party Contact, Sales Person, Remarks | `trdinqry2actions` (521,948) | Customer Inquiry > **Follow Up** (Purpose, Date, Priority, Action Taken, Action Taken Date, Person Contacted, Remarks) | `followup` (module INQ) | **521,948 / 521,948** follow-ups. Purpose 521,921, Priority 521,938, Next date 521,948, Action Taken Date 446,482 — all equal. Person Contacted 405,242 of 405,316 (see below) |
| Inquiry Follow-up / Action (screen) | Sales Person, Party, City → open actions | — (reads the actions above) | Customer Inquiry > Follow Up / pending follow-ups | — | Same data, shown from the migrated follow-ups |
| Inquiry Owner Re-Allocation / Inquiry Action Re-Allocation | Move inquiries / actions from one sales person to another | — (utility, changes the inquiry / action owner) | Customer Inquiry edit (Executive) / Follow Up | — | Utility screens with no data of their own; the current owner of every inquiry and action is migrated |
| Cold Call / Tour Plan | City, Route (Area), Profile → party list → creates inquiries | — (utility) | Customer Inquiry | — | The inquiries it created are ordinary inquiries and are migrated above |

**Not migrated (Inquiry)**

| Client data | Rows | Why |
|---|---|---|
| Item lines with no item / an item of a test office | 3 / 8 | nothing to link to a product |
| Contact Person of another party (header) | 3 of 7 | the person belongs to a different party than the inquiry |
| Person Contacted on actions | 74 (59 deleted persons + 15 of another party) | the person no longer exists in eBizWiz, or belongs to a different party |
| Quote No. / Order No. typed on the inquiry | — | not entered by the user in eBizWiz; the quotation and order link back to the inquiry |
| Upload Doc | — | document management (DMS) is out of scope |

**Notes (Inquiry)**
- eBizWiz stores the 1st action, the action taken and the next action as separate action rows; EdifyBiz keeps each of them as one follow-up of the inquiry, so the full action history is there.
- The client's inquiry list (431 on the screen) is a filtered view of the live server; the migrated copy has 193,337 inquiries in the four offices.

---

## Quotation

| Client screen | Client fields | Client DB (rows) | EdifyBiz screen | EdifyBiz DB | Result |
|---|---|---|---|---|---|
| Quotation Entry — header | Trn. No., Trn. Date, Party, Party Contact, Quote Type, Inquiry No., Sales Person, Follow Up Date, Quote Currency, Validity, Quote Status, Lost Reason, Terms & Cond., Check List, Ref. No. / Date, Remarks, Comment, Signatory, Trn. Total, Inquiry Category | `trhquote` (110,493) | Inquiry > **Customer Quotation** (form + view): Reference, Date, Customer, Customer Billing Branch, Contact Person, Quote Type, Inquiry Reference, Executive, Follow Up Date, Currency, Status, Loss Reason, Enquiry/Tender Ref No, Ref Date, Category, Remarks, Term Details | `inqcs` (type CQ) | **110,493 / 110,493**. Party 110,492, Inquiry link 110,379, Follow Up Date 110,489, Lost Reason 33,993, Ref No 109,070, Category 29,669, Validity 101,866 (in Remarks) — all equal. Status: OPEN 35,464 · LOST 49,060 · REVISED 16,237 · WON 9,732 — same as eBizWiz. Revision numbers keep their suffix (e.g. QT37439.10) |
| More > Items | Item Name / Code, Quantity, Master Rate, Discount % / Amt., Quote Rate, Tax Set | `trdquote1items` (473,651) | Customer Quotation > Product Details | `inqcsdet` | **473,641** lines, each on the right product by Item Code. Line value (qty × quote rate) **₹37,92,99,71,142.73 = eBizWiz** exactly |
| More > Post Tax Charges | Charge, Amount / Percentage, Amount | `trdquote3posttaxchgs` (324,875 on 95,323 quotations) | Customer Quotation > **Adjustments** | `inq_adjust` | Every charge with an amount is an adjustment with its name, % and amount (183,543 rows); GST % on 31,594 quotations. Grand Total = client total |
| More > Payment Terms | Payment Term, Percentage, Amount | `trdquote2payterms` (35,001 quotations) | Customer Quotation > **Term Details** | `inqcs.terms` | 35,001 / 35,001, e.g. "Payment Terms: ADVANCE PAYMENT 100% = 9447328.00" |
| More > Terms & Cond. | Term set | `trhquote.nterms` (3,916) | Term Details | `inqcs.terms` | 3,916 / 3,916 ("Terms & Cond.: ESCO Standard Terms and Conditions") |
| More > Check List | Check, Done, Date, Allotted To, Remarks | `trdquote5checks` (271,700 on 22,456 quotations) | Customer Quotation > **Task Check List** tab | `taskchecklist` (module CQ) | **271,700 / 271,700** |

**Not migrated (Quotation)**

| Client data | Rows | Why |
|---|---|---|
| Item lines of items not migrated (test offices) | 9 (+1 line with no item) | nothing to link to a product |
| Contact person pointing to a blank / missing client person | 23 | the person does not exist in eBizWiz |
| Quote For Item Groups / Item Group Heading | 10 / 1 | a screen grouping option; EdifyBiz quotation has no item groups |
| Show Items Of Selected Currency | 491 | a product-search filter, not data |
| Currency Conversion Factor | — | already applied to the rates |
| Technical Set on lines | 3 lines | empty technical data (see Master > Technical) |
| Print with Price / Print with Terms / Letterhead | — | print options chosen at print time |
| Upload Doc | — | document management (DMS) is out of scope |

**Notes (Quotation)**
- Quotations with no Sales Person in eBizWiz (165) show the Admin user as Executive, because EdifyBiz requires one.
- Signatory is kept in Remarks (683 quotations); the EdifyBiz quotation has no signatory field.
- The client list screen (238) is the live server's filtered list; the copy holds 110,493 quotations in the four offices.

---

## Order

Order Received (the customer's order) = EdifyBiz **Sales Order**. Order Placed (our order on the principal / supplier) = EdifyBiz **Purchase Order**.

### Order > Order Received → Sales Order

| Client screen | Client fields | Client DB (rows) | EdifyBiz screen | EdifyBiz DB | Result |
|---|---|---|---|---|---|
| Order Received — header | Trn. No., Trn. Date, Order From, Order By, Order Is, Order Source, Sales Person, Pending Reason, Party Contact 1 / 2, Bill To, Ship To, Inquiry Category, Terms & Cond., Check List, Client Order No. / Date, Prn. Order Ackn. No. / Date, Order Currency, Remarks, Comment, Order Status, Trn. Total | `trhordrc` (7,658) | Inquiry > **Sales Order** (form + view): Reference, Date, Customer, Order Type, Source, Executive, Contact Person, Customer Billing Branch, Customer Shipping Branch, Category, PO Number / Date, Currency, Status, Remarks, Term Details | `inqcs` (type SO) | **7,658 / 7,658**. Customer 7,654 (4 have no party in eBizWiz either), Quotation link 7,214 = eBizWiz. Status NOT DELIVERED 2,836 · PART DELIVERY 302 · FULFILLED 4,464 — same. Bill To on another party's branch 669 of 670, Ship To 7,456 of 7,458 (the missing ones point to parties deleted in eBizWiz). Ackn. No. / Date, Pending Reason, Comment in Remarks |
| More > Items | Item, Code, Master Rate, Discount, Order Rate, Quantity, Tax Set, Currency | `trdordrc1items` (35,747) | Sales Order > Product Details | `inqcsdet` | **35,731** lines, right product by Item Code; value = eBizWiz exactly. Not migrated: 2 lines of test-office items and 14 lines (₹36,819.95) of 2 orders deleted in eBizWiz |
| More > Post Tax Charges | Charge, % / Amount | `trdordrc4posttaxchgs` (24,719 on 7,059 orders) | Sales Order > Adjustments | `inq_adjust` | 14,265 adjustments; GST % on 3,157 orders; Grand Total = client total (except 2 orders whose own header ≠ their lines) |
| More > Payment Terms / Terms & Cond. | Term, %, Amount | `trdordrc3payterms` (4,548 rows on 4,023 orders) | Sales Order > Term Details | `inqcs.terms` | 4,023 / 4,023 |
| More > Check List | Check, Done, Date, Allotted To, Remarks | `trdordrc5checks` (149,662 on 4,702 orders) | Sales Order > **Task Check List** tab | `taskchecklist` (SO) | **149,662 / 149,662** (128,828 done) |
| More > Expenses | Type, Date, Amount, Remark | `trdordrc6expenses` (313 rows, 311 orders) | Sales Order > Remarks | `inqcs.remark` | 311 / 311 ("Expenses: …") |
| Items > despatch date | Delivery date per line | `trdordrc2despdate` | Sales Order line > Customer Delivery Date | `inqcsdet.customertentativedate` | 4,954 / 4,954 orders |

### Order > Order Placed → Purchase Order

| Client screen | Client fields | Client DB (rows) | EdifyBiz screen | EdifyBiz DB | Result |
|---|---|---|---|---|---|
| Order Placed — header | Trn. No., Trn. Date, Order Type, Order To, Order Placed Type, Pending Reason, Terms & Cond., Check List, Order Currency, Inquiry Category, Bill To, Ship To, Invoice No. / Date, O/A No. / Date, Remarks, Comment, Dispatch Date, Installation Date, Total Discount %, Order Status, Trn. Total | `trhordpl` (6,031) | **Purchase Order** (form + view): PO No., Date, Supplier, Customer Billing Branch, Customer Shipping Branch, Inquiry Category, Pending Reason, Invoice No. / Date, O/A No. / Date, Currency, Delivery Date, Discount, Status, Remarks, Terms | `pur_order` | **6,031 / 6,031**. Supplier 6,030 (OP1856 has no party in eBizWiz). Sales Order link 5,615. Bill To / Ship To 4,951 / 4,933 (1 each points to a deleted party). Dispatch Date → Delivery Date (250) |
| Order Placed — commission / OR payment / custom clearance block | Commission Requested / Received (+ currency), OR Value Payment Received / Pending % and Amt, Payment Pending Since Date / Days, Total INR Value of Custom Clearance, Custom Clearance Recd. / Pending Amt., CCS Remarks, Installation Date | `trhordpl` | Purchase Order > Remarks | `pur_order.remarks` | Kept with their screen labels: Commission Requested 751 / Received 239, OR received 381 / pending 189, pending since 62, installation 33 — all equal |
| More > Items | Item, Code, Quantity, Order Rate, Tax Set, Recd Qty, Remarks, Drop | `trdordpl1items` (31,364) | Purchase Order > Product Details | `pur_order_det` | **31,362** lines (the 2 others have no item), right product by Item Code, each on the product's Default Batch; value **₹24,64,85,188.29 = eBizWiz**. Line remarks 23 / 23 |
| More > Post Tax Charges | Charge, % / Amount | `trdordpl4posttaxchgs` (2,180 on 1,381 orders) | Purchase Order > Remarks | `pur_order.remarks` | 1,381 / 1,381 ("Post Tax Charges: …") |
| More > Payment Terms / Terms & Cond. | Term, %, Amount; term set | `trdordpl3payterms` (1,730 orders), `nterms` (405) | Purchase Order > Terms | `pur_order.term` | 1,730 / 405 — equal |
| More > Check List | Check, Done, Date, Allotted To, Remarks | `trdordpl5checks` (82,814 on existing orders) | Purchase Order > **Task Check List** tab | `taskchecklist` (PUR) | **82,814 / 82,814** (46,070 done) |
| More > Add Against Order Received Items | Picks Sales Order lines into the PO | — (screen action) | Purchase Order > Sales Order link | `pur_order.salesordercode` | The link it creates is migrated (5,615 POs) |

**Not migrated (Order)**

| Client data | Rows | Why |
|---|---|---|
| Order lines of orders deleted in eBizWiz | Sales Order 14 lines (2 orders); PO check items 1,124 (58 orders) | their order header no longer exists in eBizWiz |
| Disp. Qty. / Recd. Qty. / Status / Drop on lines | — | fulfilment counters; EdifyBiz works them out from the migrated invoices / GRNs |
| Order Type (ON PARTY) / Order From (FROM PARTY) / Order Placed Type | all | screen selectors with one value; AGAINST ORDER RECEIVED is the Sales Order link |
| Show Items that reached MOL / ROL, Show Parts Required of Service Calls | — | item-search filters, not data |
| Order Received from another office / by call (3 orders) | 3 | migrated as ordinary customer orders |
| Letterhead / print options, Upload Doc | — | print options; document management (DMS) is out of scope |

**Notes (Order)**
- **Purchase Order charges are text only.** The Saksham PO screen shows no Adjustments and a PO carries no tax, so the client's post-tax charges are listed in the PO Remarks and are **not** included in the EdifyBiz PO total.
- Commission Requested / Received are masked on the client screen (XXXXXXXX) but stored in eBizWiz; they are kept in the PO Remarks as decided.
- The client list screens (Order Received 28) are live filtered lists; the copy holds 7,658 Order Received and 6,031 Order Placed in the four offices.

---

## Stock

| Client screen | Client fields | Client DB (rows) | EdifyBiz screen | EdifyBiz DB | Result |
|---|---|---|---|---|---|
| Stock-IN | Trn. No., Trn. Date, Stock In From (Party / Office / User), From, Stock In Is (Fresh / Against Order Placed / Against Stock Out / Against Sales Return), Dispatch Mode, Dispatch Through, Docket No. / Date, Terms & Cond., Check List, Ref. No. / Date, Remarks, Comment, Trn. Total; items: Item, Code, Master Rate, Discount, Stock In Rate, Challan Qty., Qty. Received, Qty. Missing, Qty. Accepted (Good), Qty. Rejected (Def.), Reject Reason, Tax Set; More: Post Tax Charges, Payment Terms, Serial No. | `trhstkin` (7,150) + `trdstkin1items` (30,583) | Inventory > **GRN** (receipts from a party or a user); office-to-office receipts inside the matching **GTA** | `stockinout` (GRN) + `stockinoutdet`, `stockb2b`, `stocktrans` | **7,150 / 7,150**: 6,433 GRN (4,754 linked to their Purchase Order) + 717 received inside a GTA. Good qty to stock, rejected qty as damaged stock. Dispatch mode / through, docket, ref, charges, payment terms, missing qty and serial numbers are kept in the GRN Remarks |
| Stock-OUT | Trn. No., Trn. Date, Stock Out To (Party / Office / User), To, Stock Out Is, Dispatch Mode / Through, Docket No. / Date, Terms & Cond., Check List, Ref. No. / Date, Remarks, Comment, Trn. Total; items with quantity and defective quantity | `trhstkou` (1,326) + `trdstkou1items` (2,475) | Inventory > **MRO** (issue to a user or party); **GTA** (office-to-office transfer) | `stockinout` (MRO), `stockb2b`, `stocktrans` | **1,326 / 1,326**: 559 MRO + 767 GTA (50 still in transit, as in eBizWiz). Office transfers move the stock from the sending to the receiving branch |
| Write Off Missing Quantity | Service Person, Item Category, Status → missing quantities to write off | — (no transaction table; it changes the missing balance) | — | — | Utility with no document of its own; its effect is in the client's stock balance, which EdifyBiz now matches (see Master > Item) |
| Physical Stock Taking | Trn. No., Trn. Date, Party, Ref. No. / Date, Remarks + counted items | `trhpstk` (5 in the four offices) + `trdpstk1detail` (0) | — | — | Nothing to migrate: the 5 headers have no counted items |
| (stock sold, call spares) | Sales invoices, spare parts used on calls | `trdsales1items`, `trdcalls3parts` | Sales Invoice / Complaint spares MRO | `stocktrans` (SAL, MRO) | 18,629 invoice lines reduce stock as EdifyBiz's own sales invoice does; 106 spare-part MROs for calls |
| (opening + current balance) | Item Opening Balance, client stock balance | `mststkdt` | Inventory > Opening Stock; Stock Summary | `stockjv` (JV) | Opening 2,589 rows + balance adjustment 2,501 rows. **Stock per item + branch = eBizWiz on 4,352 / 4,352** |

**Not migrated (Stock)**

| Client data | Rows | Why |
|---|---|---|
| Physical Stock Taking | 5 headers, 0 lines | nothing counted |
| Stock transfer user → user | 1 header, 0 lines | nothing moved |
| Serial master entries of Stock-IN | 22 | kept in the GRN remark; the EdifyBiz serial master has no GRN link |
| Upload Doc | — | document management (DMS) is out of scope |

**Notes (Stock)**
- The GRN, MRO and GTA screens are used as they are (no form change). Client fields with no place on them (dispatch, docket, reference, charges, payment terms, missing qty, serial numbers) are kept, labelled, in the document Remarks.
- Stock-IN and Stock-OUT between two offices with the same items become one GTA, so each branch moves exactly what eBizWiz moved; 25 pairs with different items stay as a separate MRO and GRN.
- Users see GRN / MRO / GTA of the branches they have access to (from eBizWiz user–office rights).

---

## Sales

Warranty Sales and Non Warranty Sales (Spare Part Sale Entry) = EdifyBiz **Sales Invoice**, labelled "Warranty Sales" / "Non Warranty Sales". The warranty serials and their PM visits = EdifyBiz **Warranty contract** (AMC contract of type Warranty) with its PMS schedule.

| Client screen | Client fields | Client DB (rows) | EdifyBiz screen | EdifyBiz DB | Result |
|---|---|---|---|---|---|
| Warranty Sales / Non Warranty Sales — header | Trn. No., Trn. Date, Party, Party Contact, Sales Type, Sales Person, Bill To, Ship To, Campaign, Sales Source, P.O. No. / Date, Dispatch Mode / Through, Docket No. / Date, Terms & Cond., Check List, Ref. No. / Date, Remarks, Comment, Demo, Trn. Total | `trhsales` (5,707) | GST Sales > **Sales Invoice** (form + view): Invoice No., Date, Customer, Contact Person, Sales Type, Executive, Customer Billing Branch, Customer Shipping Branch, P.O. No. / Date, Dispatch Mode / Through, Docket No. / Date, Terms, Remarks | `sal_order` + label | **5,707 / 5,707**: Warranty Sales 4,313, Non Warranty Sales 1,394 — same as eBizWiz. Customer 5,707, header fields 0 mismatch, Bill To / Ship To to the right party. Sales Source (113) and Demo (15) in Remarks. 3,327 invoices linked to their Sales Order / Quotation |
| Sales Items | Item, Item Code, Quantity, Master Rate, Discount % / Amt., Warr. Sale Rate (WS Rate), Tax Set, Main Item, Auto Generated | `trdsales1items` (18,694) | Sales Invoice > Product lines (Product, Quantity, Price, GST %) | `sal_order_det` | **18,693** lines (1 test-office item), right product by Item Code, Default Batch. Value (qty × WS rate) **₹1,54,78,55,451.71 = eBizWiz** exactly |
| More > Post Tax Charges | Charge, % / Amount | `trdsales4posttaxchgs` (11,799 on 3,193 invoices) | Sales Invoice > CGST / SGST / IGST per line + Adjustments | `sal_tax`, `sal_adjust` | GST on 2,063 invoices (9,495 tax rows, tax type follows the customer's state); other charges as Adjustments. Grand Total = client total on 4,606 of 4,796; the 190 others are 2017 invoices whose own client total does not add up |
| More > Payment Terms / Terms & Cond. | Term, %, Amount; term set | `trdsales3payterms` (1,307 invoices), `nterms` (49) | Sales Invoice > Terms | `sal_order.term` | 1,307 / 49 — equal |
| More > Check List | Check, Done, Date, Allotted To, Remarks | `trdsales6checks` (56) | Sales Invoice > Task Check List tab | `taskchecklist` (SAL) | 56 / 56 |
| Sales Items > View Serial No. | Serial No., Start / End Date, Months, PM Visits, Location, Installation Date, Close, Main Item | `trdsales2itemsdet` (6,937) | **Warranty contract** lines (AMC > Contract, type Warranty); also printed under the product on the invoice | `amc` (type Warranty), `contractdetails`, `sal_order_det.prod_desc` | **4,261** warranty contracts (one per invoice with serials) with **6,936** serial lines — warranty start / end, months, installation date, location equal |
| Reschedule PM Visits (Sales and Contracts menus) | Warranty items with serial, party, PM visits → change visit dates | `trdsales7pmvisit` (5,131: 2,029 done) | AMC > Complaint / **PMS** list and calendar | `contractcall` (type PMS) | **5,131 / 5,131** PM visits: 2,029 Closed, 2,196 Cancelled (their call was cancelled in eBizWiz), 908 Open (753 of them in the future). The screen itself is a utility (changes dates); the current dates are migrated |
| Contract Sign-Up Advice | List of warranty items to offer an AMC | — (report, no table) | AMC contracts / warranty expiry | — | Report screen with no data of its own; it works from the migrated warranty contracts |

**Not migrated (Sales)**

| Client data | Rows | Why |
|---|---|---|
| Item line of a test-office item | 1 (+1 serial) | nothing to link to a product |
| Contact person of another party | 6 | the person belongs to a different party than the invoice |
| Main Item / Auto Generate Dummy Serial No. | 5,353 / 2,634 lines | entry helpers for bundles and dummy serial numbers; no field on the EdifyBiz invoice line |
| Technical Set | 0 lines | no data |
| Letterhead / print options, Upload Doc | — | print options; document management (DMS) is out of scope |

**Notes (Sales)**
- In EdifyBiz a warranty is an AMC contract of type **Warranty**, linked to its invoice; it is used by service calls exactly like an AMC (a call on that serial inside the warranty period is under warranty).
- Serial numbers with warranty dates are also printed under each product on the invoice, so the invoice shows the same serial information as eBizWiz.
- 190 old (2017) invoices have a client total that does not equal their own lines and charges; EdifyBiz shows the correct total of the migrated lines and charges.

---

## Contracts

| Client screen | Client fields | Client DB (rows) | EdifyBiz screen | EdifyBiz DB | Result |
|---|---|---|---|---|---|
| Contract Sales (Contract Entry) — header | Trn. No., Trn. Date, Party, Party Contact, Sales Person, AMC Quote No., Pymt. Schedule, B / E of Period, Campaign, Sales Source, P.O. No. / Date, Terms & Cond., Check List, Ref. No. / Date, Remarks, Comment, Trn. Total | `trhcontr` (4,528) | AMC > **Contract** (form + view): Contract Number, Contract Date, Customer, Customer Billing Branch, Contact Person, Executive, Quotation, Payment Terms, AMC Type, Start / End Date, PO Number / Date, Remark | `amc` | **4,528 / 4,528**. Quotation link 4,165, Sales Person 4,023, PO 21 — equal. Payment Terms = schedule + B / E of period (e.g. "Half Yearly - Beginning of Period"). Renewals linked to the previous contract (2,935). Terms & Cond., Sales Source, ref, charges, eBizWiz total and amount received in the Remark |
| Contract Items + View Sr. No. | Contract Is (Fresh / Transfer From Contract / Transfer From Warranty), Item, Code, Quantity, Main Item; serial, rate, location, start / end, months, PM visits, first installation | `trdcontr1items` + `trdcontr2itemsdet` (7,187 serials) | Contract > Product / serial lines | `contractdetails` | **7,186** serial lines (1 serial row has no item line), right product by Item Code. Contract amount = Σ serial rates on 4,528 / 4,528. Warranty-transfer serials keep their invoice no. / date (1,113). Contract Is also fills the header **Contract Type** (list + view), master AMC Contract / Contract Type |
| More > Payment Schedule; Contract Schedule Bills - Generate | Bill dates and amounts per contract | `trdcontr3bills` (7,618) | Contract > **Billing Cycle** tab (Billed / Unbilled) | `billingcycle` | **7,618 / 7,618** bills, ₹22,60,67,772.51 |
| (billed bills) Contract Schedule Bills - Print | The bills that were generated / printed | `trdcontr3bills` with a bill no. (4,000) | GST Sales > **Sales Invoice** (label "AMC Contract Bill"), linked to its billing cycle | `sal_order`, `billingcycle.salcode` | **3,996** invoices (4 bills of amount 0 not invoiced), total **₹11,70,73,427.85 = eBizWiz billed bills**. GST from the contract charges; 14 bills carry a "Bill difference (eBizWiz)" line so the invoice equals the client bill. Receipts are allocated to these invoices |
| More > Post Tax Charges | Charge, % / Amount | `trdcontr4posttaxchgs` (6,012 on 3,858 contracts) | AMC bill invoices (GST + Adjustments); Contract Remark | `sal_tax`, `sal_adjust`, `amc.remarks` | Charges applied to each bill invoice in proportion; also listed in the contract Remark |
| Reschedule PM Visits | Contract serial, party, PM visits → change visit dates | `trdcontr7pmvisit` (14,219, 11,502 done) | AMC > Complaint / **PMS** list and calendar | `contractcall` (type PMS) | **14,219 / 14,219** PM visits, 11,502 Closed; PM VISIT calls update their own PMS row |
| Contract Renewal Advice | Contracts due for renewal | — (report) | AMC contract end dates / renewals | — | Report screen; works from the migrated contract periods |
| AMC Quotation (AMC Quotation Entry) | Trn. No., Trn. Date, Party, Party Contact, Sales Person, Follow Up Date, Offer Type, Conversion, Pymt. Schedule, B / E of Period, Terms & Cond., Check List, Ref. No. / Date, Remarks, Comment, Trn. Total; items: Offer Is, Item, Code, Quantity, serials; More: Post Tax Charges, Payment Schedule, Check List | `trhoffer` (8,758) + items / serials (13,541) | Inquiry > **Customer Quotation** (AMC Contract) | `inqcs` (CQ, AMC Contract), `inqcsdet`, `inq_adjust`, `taskchecklist` | **8,758 / 8,758**, **13,541** serial lines (serial no., location equal). Status: Contract Signed 3,999 · Closed 4,362 · Partly Contract Signed 126 · Open 271. Terms (Payment Schedule 8,758, term set 1,436), check list 25,833 — equal. Contracts link back to their AMC quotation |
| AMC Quotation - Range Print | Print quotations for a date range | — (print) | Customer Quotation print | — | Print utility, no data of its own |

**Not migrated (Contracts)**

| Client data | Rows | Why |
|---|---|---|
| Sales Invoice for zero-amount billed bills | 4 | nothing to invoice; the billing-cycle row exists |
| Contact person pointing to a blank / missing client person | contracts 34, AMC quotations 38 | the person does not exist in eBizWiz |
| Campaign, contract check items, technical set, T&C text | 0 | empty in eBizWiz |
| Main Item / Auto Generated / Technical Set on lines | — | entry helpers; one EdifyBiz line per serial |
| Letterhead / print options, Upload Doc | — | print options; document management (DMS) is out of scope |

**Notes (Contracts)**
- Contract bills are invoiced through the contract's **Billing Cycle** (EdifyBiz standard), not on the contract itself, so editing a contract never removes its invoices.
- PM visits without a call in eBizWiz (2,176) stay as scheduled PMS rows (Open), as in eBizWiz.
- 8 contracts (e.g. MC316, MC580, MC1527–MC1529) have no serial in eBizWiz; they are migrated as contract headers without lines, so they have no AMC type or period.
- Warranty (from Warranty Sales) is also an AMC contract, of type Warranty — see the Sales section.

---

## Service

| Client screen | Client fields | Client DB (rows) | EdifyBiz screen | EdifyBiz DB | Result |
|---|---|---|---|---|---|
| Call Entry (Service Entry) — header | Unique Call No., Trn. No., Date, Call Logged By, Call Type; Party, Telephone 1 / 2, Person Calling, Transfer To, Contact Person; Item, Item Code, Serial No., Location, Item Status [W-C-O], Purchase / Start / End Date, Manufacturer; Complaint Reported, Our Complaint, Special Instructions, Product Condition; Service Person, Allocation / Appointment / Accepted Date; Repair Estimate, Chargeable Amount, Bill No.; Pending / Cancel Reason, Follow Up, Fast Close, Solved, Solved Date / Remarks, Comment | `trhcalls` (36,164) | AMC > **Complaint** (form + view): Complaint No., Report No., Complaint Date, Customer, Contact Person, Product, Serial Number, Contract Number, Complaint, Service Complaint, Observation, Assigned To, Status, Call Amount, Service Details, Remark; Label = call type | `contractcall` | **36,164 / 36,164**. Status: Closed 31,169 (= solved in eBizWiz), Cancelled 4,767, Open 228 — equal. Our Complaint 28,681, Service Person 32,512, Special Instructions → Observation 1,512, Solved Remarks → Service Details 6,640, Chargeable Amount 1,215 — all equal. 30,414 calls linked to their AMC / warranty contract line; 5,750 Walk In. Logged by, person calling, transfer to, pending / cancel reason, comment in Remark |
| Call Entry — More > Visit / Work Details | Visit date / time, engineer, fault (complaint, defect, repair), remarks | `trdcalls1visit` (56,248) + `trdcalls2fault` (32,522) | Complaint > **Call Visit Logs** | `callvisits` | 56,248 / 56,248 visits; each fault written into its visit's remark |
| Call Entry — More > Parts Required / used parts | Spare parts used, quantity, rate; pending parts | `trdcalls3parts` (6,858), `trdcalls10pendingparts` (5) | Complaint > **Used Product** / Requested Product; spare-part MRO | `amcusedproduct`, `amcrequestedproduct`, `stockinout` MRO | 6,847 used (11 items not migrated), 5 requested; 106 spare-part MROs reduce stock |
| Call Entry — More > Check List | Check, Done, Date, Allotted To | `trdcalls8checks` (224,583) | Complaint > **Task Check List** tab | `taskchecklist` (ACM) | 224,583 / 224,583 |
| Call Entry — Bill No. (chargeable calls) | Bill No. / Date, amount, post-tax charges | `trhcalls` bill + `trdcalls6posttaxchgs` | GST Sales > **Sales Invoice** ("Service Call Bill") | `sal_order` | **960** invoices, ₹2,41,96,132.26 = eBizWiz billed calls (23 bills of ₹0 not invoiced). Receipts allocated to them |
| Customer Feedback / Survey | Q1–Q12 answers per call | `trdcalls5happy` (41,256 answers, 3,438 surveys on 3,420 calls) | Complaint view > **Customer Rating** + Customer Feedback Remark | `customerfeedback` | 3,420 / 3,420 calls; each answer a line "Q: … - A: …"; CSI % gives the 1–5 star rating |
| Call Allocation | Route, Call Type, Allocation Type, call-wise / engineer-wise → assign engineer | — (utility; writes the call's service person) | Complaint > Assigned To | `contractcall.assignedto` | The engineer of every call is migrated (32,512) |
| Call Sheets - Range Print | Print call sheets for a date range | — (print) | Complaint print | — | Print utility, no data of its own |
| Assign Service Routes | Service Person → Routes (Areas) | `msduserroute` (0) | — | — | Nothing to migrate: no route is assigned in eBizWiz |
| Search Serial Number | Serial → its sale / contract / calls | — (search) | Complaint / Contract search by serial | — | Search screen; works on the migrated serials |
| Global Pending Calls | Party, dates, call no. → open calls | — (list) | Complaint list, status Open | — | Same data (228 open calls) |
| Claim Form Entry | Claim Type, Claim On, Ref., Remarks, claim items | `trhclaim` (0 in the four offices; 3 in test offices) | — | — | Nothing to migrate |

**Not migrated (Service)**

| Client data | Rows | Why |
|---|---|---|
| Used parts of items not migrated | 11 | item not in the product master (test office) |
| Call bills of amount 0 | 23 | nothing to invoice; bill no. kept in the call remark |
| Repair Estimate / Estimate Amt / Approved By / Customer PO | 0 in the four offices | no data |
| Outside Repair, stand-by, check-visits | 0 | empty in eBizWiz |
| Letterhead, Upload Doc | — | print option; document management (DMS) is out of scope |

**Notes (Service)**
- A call is linked to the serial's AMC or warranty line only when the call date falls inside that contract period; otherwise it is a Walk In call, so warranty / AMC status on each call is correct.
- PM VISIT calls update their own scheduled PMS row instead of creating a new complaint (17,174 PM rows closed by their call).
- Call status follows eBizWiz: Solved → Closed (with closing date), cancel reason → Cancelled, else Open.

---

## Receipts

| Client screen | Client fields | Client DB (rows) | EdifyBiz screen | EdifyBiz DB | Result |
|---|---|---|---|---|---|
| Receipt Entry (Payment Receipt Entry) — header | Trn. No., Trn. Date, Party, Deposit Bank, Ref. No. / Date, Terms & Cond., Remarks, Amount Recd. | `trhpayrg` (3,250; 2,841 with a line) | **Payment** (receipt): Payment No., Date, Customer, Bank/Cash, Reference, Remark, Amount | `payments` | **2,841** receipts → **2,880** payments (one per cheque / NEFT, as EdifyBiz records a payment per instrument). Amount **₹18,10,20,531.23 = eBizWiz**, TDS ₹16,83,468.24. Deposit bank → company bank (BOB / SBI); 195 receipts have no deposit bank in eBizWiz |
| More > Receipt Details | Payment of (Sales / AMC / Calls), Bill No., Mode Amount, Mode No., Mode Date, Drawn On, TDS / WCT / Other Deduction, Payment Mode | `trdpayrg1details` (3,565) | Payment > allocation to invoices (instrument no., date, bank, TDS) | `paymentdetails` | **3,565 / 3,565** lines, each on its own invoice: Sales Invoice 697 (₹10,40,89,500.32), AMC bill invoice 2,429 (₹6,76,60,969.02), Service Call invoice 438 (₹92,70,061.89), 1 line on account (amount 0) |
| (bill status) | Paid / partly paid bills | — | Invoice outstanding / Billing Cycle Billed tab | — | AMC bills with a receipt 2,389 (2,341 paid exactly, 2 over-paid in eBizWiz itself); call bills 430 (405 paid exactly); Sales Invoices 677 (644 fully paid) — same as eBizWiz. No false customer advance created (₹0) |

**Not migrated (Receipts)**

| Client data | Rows | Why |
|---|---|---|
| Receipts with no line | 409 | no amount and no document in eBizWiz either |
| Terms & Cond. on receipts | 0 | empty |
| Upload Doc | — | document management (DMS) is out of scope |

**Notes (Receipts)**
- Every receipt is allocated to the exact invoice it paid in eBizWiz, so customer outstanding, AMC Billed / Unbilled and call billing in EdifyBiz match eBizWiz.
- A receipt with several cheques becomes one EdifyBiz payment per cheque (2,841 receipts → 2,880 payments), with the same total.
- EdifyBiz marks the instrument "Branch" as required on the payment form; it matters only if a user edits a migrated payment (eBizWiz has no such field).
