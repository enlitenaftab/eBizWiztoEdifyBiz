# 01. Party Master → Contact

Format: see `00_MAPPING_FORMAT.md`.

## 1. Overview

| | Client (eBizWiz) | Ours (EdifyBiz) |
|---|---|---|
| Screen / menu | **Master → Party** — *Party Master* (header) + *Party Detail* (contact persons) | **Contact** (add/edit form, view page, Person tab) |
| Tables | `mstparty` (party), `msdparty` (persons); masters `mstfixedselection` (title, communication), `mstpartytype`, `mstpartyprofile`, `mstcity`, `mstroute`, `mstusers`, `mstdesignation`, `mstoffice` | `contact`, `mltaddress` (address / branch), `mltcontact` (persons); masters `miscellaneous` (Contact / Party Type, Party Profile, Communication), `desigmaster`, `city` / `state` / `country`, `users`, `companyaddress` |
| Code | — | form + view `contact/default.asp`, JS `assets/scripts/contact.js`, backend `app/contact.asp`, dropdown helpers `app/UIFunctions.asp`, designation search `app/selectjson.asp` |
| Exe | — | module **1. Contact** — class `Contact` in `Program.cs` |
| Scope | offices 2 HO Mumbai, 3 Bangalore, 4 Delhi, 6 Kolkata (1 and 5 are WinMax test offices) | |
| Status | | migrated and verified |

---

## 2. Field mapping

### 2.1 Party Master → `contact`

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 1 | Name | `mstparty.vname` | `contact.name` | Name | page title | title prefixed (see 2) |
| 2 | Title | `mstparty.ntitle` → `mstfixedselection` | part of `contact.name` | — | — | "M/s. Aastrid International Pvt. Ltd." |
| 3 | Party Type | `npartytype` → `mstpartytype.vname` | `contact.ctype` | **Party Type** ✚ | **Party Type** ✚ | name as text |
| 4 | Party Profile | `npartyprofile` → `mstpartyprofile.vname` | `contact.companygroup` | **Party Profile** ✚ (dropdown) | **Party Profile** (label) | name as text |
| 5 | Sales Person | `nusersales` → `mstusers.vname` | `contact.crmagent` | **Sales Person** ✚ | **Sales Person** ✚ | user by name |
| 6 | Service Person | `nuserservice` → `mstusers.vname` | `contact.fieldagent` | **Service Person** ✚ | **Service Person** ✚ | user by name |
| 7 | Tax1 | `vtax1` | `contact.pan` | — | — | cap 50; full text also in Remarks |
| 8 | Tax2 | `vtax2` | `contact.gst_no` | — | — | cap 50; full text also in Remarks |
| 9 | Tax3 | `vtax3` | `contact.cst` | — | — | cap 50; full text also in Remarks |
| 10 | Reminder Message | `vremindermessage` | `contact.custom1` + `contact.remark` | — | Remark section | also appended to Remarks as `Reminder Message: …` |
| 11 | Remarks | `vremarks` | `contact.remark` | — | Remark section (edited on the view) | + `Tax1: … \| Tax2: … \| Tax3: … \| Reminder Message: …` appended |
| 12 | Active | `bactive` | `contact.isdeleted` | — | — | inverted: active 1 → `isdeleted` 0 |
| 13 | (office of the record) | `nofficeid` → `mstoffice` | `contact.companybranch` | — | — | office → `companyaddress.code` (branch) |
| 14 | — | `addedon` / `editedon` | `createdon` / `updatedon` | — | — | `createdby`, `updatedby`, `owner` = migration user |

### 2.2 Party Master address → `mltaddress` (install row: `isdefault = 1`, `isshipto = 1`)

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 15 | City | `ncity` → `mstcity.vname` | `mltaddress.citycode` | City | City | city by name; missing cities / states / countries seeded |
| 16 | Route (Area) | `nroute` → `mstroute.vname` | `mltaddress.area` | **Area** (saksham + JMCA gate) | — (view gate is JMCA only) | name as text |
| 17 | — | — | `mltaddress.place` | Place/Branch/Office | — | city name, else route, else office name |
| 18 | Address | `vinstaddress` | `mltaddress.address` | Address | Address | |
| 19 | Postal Code | `vinstpostalcode` | `mltaddress.pincode` | Pincode | Pincode | |
| 20 | Telephone 1 | `vinsttel1` | `mltaddress.telephone1` | Landline | Landline | |
| 21 | Telephone 2 | `vinsttel2` | `mltaddress.telephone2` | Landline (2nd box) | Landline | |
| 22 | Fax | `vinstfax` | `mltaddress.fax1` | — (Additional Addresses → Fax) | Fax | |
| 23 | Email | `vinstemail` | `mltaddress.email1` | Email | Email | |
| 24 | Website | `vwebsite` | `mltaddress.website1` | Website | Websites | |

### 2.3 Billing address → second `mltaddress` row (`isbillto = 1`)

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 25 | Copy same as above | — | — | — | — | not stored; a billing row is written only when the billing address is filled, differs from Address and is not "same as above" |
| 26 | Billing Address | `vbilladdress` | `mltaddress.address` | Additional Addresses → Address | Additional Addresses tab | same city as the install row |
| 27 | Postal Code / Telephone 1 / Telephone 2 / Fax / Email | `vbillpostalcode`, `vbilltel1`, `vbilltel2`, `vbillfax`, `vbillemail` | `pincode`, `telephone1`, `telephone2`, `fax1`, `email1` | Additional Addresses → same fields | Additional Addresses tab | |

### 2.4 Party Detail → `mltcontact` (one row per person; first person `isdefault = 1`)

| # | Client UI | Client DB | Our DB | Our Form (Person) | Our View (Persons grid: Name · Branch · Designation / (Department) · Direct Line · Mobile · Landline · Email ID) | Rule |
|---|---|---|---|---|---|---|
| 28 | Contact Person | `msdparty.vcontactperson` | `mltcontact.name` | Name | Name | title prefixed, cap 100 |
| 29 | Title | `ntitle` → `mstfixedselection` | part of `name` | **Title** ✚ | — | the form's Title is prefixed into the name on save (same rule) |
| 30 | Designation | `ndesignation` → `mstdesignation`, else `vdesignation` | `mltcontact.designation` | **Designation** (search) ✚ | Designation / (Department) | master seeded into `desigmaster` |
| 31 | Telephone 1 | `vtel1` | `mltcontact.telephone1` | Landline | Landline | kept as landline, never promoted to Mobile |
| 32 | Telephone 2 | `vtel2` | `mltcontact.telephone2` | Landline (2nd box) | — | |
| 33 | Mobile | `vmobile` | `mltcontact.mobile1` | Mobile | Mobile | |
| 34 | Email | `vemail` | `mltcontact.email1` | Email | Email ID | |
| 35 | Date of Birth | `ddob` | `mltcontact.bday` | Birthday | — | |
| 36 | Anniversary Date | `danniversary` | `mltcontact.anniversary` | Anniversary | — | |
| 37 | Spouse Name | `vspousename` | `mltcontact.otherinfo` | Remark | — | written as `Spouse: <name>` |
| 38 | Date of Birth (spouse) | `dspousedob` | `mltcontact.spousebday` | Spouse's Birthday | — | |
| 39 | Communication Model | `ncommunication` → `mstfixedselection` | `mltcontact.communication` | **Communication Model** ✚ | — | master seeded into `miscellaneous` |
| 40 | Date of Birth (child 1) | `dchild1dob` | `mltcontact.firstchildbday` | 1st Child's Birthday | — | |
| 41 | Date of Birth (child 2) | `dchild2dob` | `mltcontact.secondchildbday` | 2nd Child's Birthday | — | |
| 42 | Remarks | `vremarks` | `mltcontact.otherinfo` | Remark | — | before the spouse line |
| 43 | — | — | `mltcontact.branch` | Branch | Branch | the contact's install `mltaddress` code (no per-person branch in eBizWiz) |

✚ = field added to our form / view for saksham (§4a).

---

## 3. Masters seeded

| Master | Our table | Rows | Source |
|---|---|---|---|
| Designation | `desigmaster` | 7,677 client designations (all active) + free-text designations of persons | every active `mstdesignation` + the ones used by persons |
| Communication Model | `miscellaneous` (Contact / Communication) | 16 | `mstfixedselection` |
| Party Type | `miscellaneous` (Contact / Party Type) | 10 | every active `mstpartytype` (all 10 are active) + any inactive one used by a party |
| Party Profile | `miscellaneous` (Contact / Party Profile) | 40 | every active `mstpartyprofile` (all 40 are active) + any inactive one used by a party |
| Users | `users` | 309 created (311 in target) | all `mstusers` — every Sales / Service person resolves; a user inactive in eBizWiz gets `suspenduser = 1` (104); `mobile_no` / `zipcode` from `vmobile` / `vpostalcode`; role and password = the migration user's (client rights are role/menu based and are not mapped) |
| User branch access | `users.companybranch` | set on every user with a migrated office (255 users, §6) | `msduseroffice` (user ↔ office) + the user's own `mstusers.nofficeid`, offices 2/3/4/6 → branches 3/4/5/6, format "3, 4"; Admin (migration user) gets all four; existing values kept, only missing branches added |
| City / State / Country | `city`, `state`, `country` | +252 / +15 / +2 | cities not in the target master |
| Company branches | `companyaddress` | 4 | offices 2, 3, 4, 6 (`place` = office name); `Shared.FillBranchDetails` fills the empty address, pincode, telephone 1–3, fax, email, website, zone, remark from `mstoffice` and the **Office Type** (`type`) |
| Office Type | `miscellaneous` (BDS / Office Type) — Company Address "Office Type" dropdown | 6 | every active `mstofficetype` (HEAD OFFICE, BRANCH OFFICE, WAREHOUSE, FACTORY, REGIONAL OFFICE, REPRESENTATIVE OFFICE) |

---

## 4. Changes made on our side

### 4a. Form / view changes (saksham-gated)

| # | Screen | Change | Kind | File | DB column |
|---|---|---|---|---|---|
| 1 | Contact form | **Party Type** dropdown + quick-add (+) | New field | `contact/default.asp` (saksham row above Name) | `contact.ctype` |
| 2 | Contact form | **Sales Person** dropdown | New field | same row | `contact.crmagent` |
| 3 | Contact form | **Service Person** dropdown | New field | same row | `contact.fieldagent` |
| 4 | Contact form | "Company Group" free text → **Party Profile** dropdown + quick-add | Label + New field | `contact/default.asp` (Party Profile block) | `contact.companygroup` |
| 5 | Contact form | **Area** field shown for saksham (JMCA gate extended) | Layout | `contact/default.asp` (Area block) | `mltaddress.area` |
| 6 | Contact view | Label "Company Group" → **Party Profile** | Label | `contact/default.asp` (Location section) | `contact.companygroup` |
| 7 | Contact view | New row **Party Type · Sales Person · Service Person** | New field | `contact/default.asp` (saksham block after Location) | `ctype`, `crmagent`, `fieldagent` |
| 8 | Contact view | Party Type filled from the contact array | Behaviour | `assets/scripts/contact.js` (`contact_view_ctype` ← `aContact[118]`) | `contact.ctype` |
| 9 | Contact view | `crmagent`, `fieldagent`, `ctype` added to the view query | Backend | `app/contact.asp` (`,c.crmagent, c.fieldagent, c.ctype as ctypevalue`) | — |
| 10 | Person form | **Title** dropdown (Mr./Mrs./Ms./Dr./M/s./Prof.), prefixed into Name on save | New field + Behaviour | `contact/default.asp` (person row), `contact.js` (addperson) | `mltcontact.name` |
| 11 | Person form | Designation free text → **searchable designation** (existing + `desigmaster`) | New field | `contact/default.asp`, `app/selectjson.asp` (`searchdesignation`) | `mltcontact.designation` |
| 12 | Person form | **Communication Model** dropdown + quick-add | New field | `contact/default.asp` (person row) | `mltcontact.communication` |
| 13 | Dropdown helpers | `partyTypeSelect`, `partyProfileSelect`, `communicationModelSelect` (used values ∪ `miscellaneous` master) | Backend | `app/UIFunctions.asp` | — |

**New fields on the form: 7** (Party Type, Sales Person, Service Person, Party Profile dropdown, Person Title, searchable Designation, Communication Model) · **on the view: 3** (Party Type, Sales Person, Service Person) · labels: 2 · other layout / behaviour / backend: 4.

### 4b. Database changes (ALTER)

| Statement | Why |
|---|---|
| `ALTER TABLE contact ALTER COLUMN pan nvarchar(50)` | Tax1 is labelled free text up to 49 characters ("PAN - …"); the native length 30 would cut 4 rows |
| `ALTER TABLE contact ALTER COLUMN gst_no nvarchar(50)` | Tax2 ("GSTIN - 24AAACC6253G1ZZ") — the native varchar(15) would cut 53 rows |
| `ALTER TABLE contact ALTER COLUMN cst nvarchar(50)` | Tax3, same pattern |

---

## 5. Not migrated

| Client UI | Client DB | Rows | Why |
|---|---|---|---|
| Party with no name | `mstparty.vname` empty | 1 | nothing to show a contact as |
| Persons with no party | `msdparty.nparty` empty | 311 | not linked to any party in eBizWiz |
| Person of a test-office party | `msdparty` whose party sits in office 1 | 1 | parent party is out of scope |
| Party Profile: Email Instance | `mstpartyprofile.bemail` | 40 | our Party Profile master (`miscellaneous`) has no such flag |
| Party Type: Common | `mstpartytype.bcommon` | 2 types (PRINCIPAL, test party) | not needed: saksham contacts are not branch-wise (company setting `contactbranchwise = 0`), so every contact is visible to every branch |
| Party Info tab | `msdpartyinfo` | 0 | empty in eBizWiz |
| Party defaults per office | `defparty` | 7 | form default settings (default type / profile / city, allowed sales & service persons), not party data |
| Child 1 Name / Child 2 Name | `msdparty.vchild1`, `vchild2` | 9 persons | `mltcontact` has no child-name column (the birthdays migrate) |
| Person Active | `msdparty.bactive` | 72 inactive | all persons kept active |
| TPC | `msdparty.btpc` | 43 | skipped by decision |
| Copy same as above | UI checkbox | — | not a stored value |

---

## 6. Verification (latest run — 0 errors)

| Check | Client | Ours |
|---|---|---|
| Parties → contacts | 20,757 (1 without a name) | **20,756** migrated (`contact` 20,758 incl. 2 template rows) |
| Persons → `mltcontact` | 83,941 named: 311 with no party, 1 under a test-office party | **83,629** migrated (`mltcontact` 83,631 incl. 2 template rows) |
| Addresses → `mltaddress` | — | **20,763** (install rows + 7 separate billing addresses) |
| Party Type / Party Profile / Sales / Service person | — | every value resolved to a master / user (0 unresolved) |
| Tax1/2/3 kept in Remarks | — | 139 contacts carry `Tax1:` / `Tax2:` / `Tax3:` in `contact.remark` |
| Reminder Message kept in Remarks | 446 parties | `Reminder Message:` in `contact.remark` (from the next run) |
| User branch access | 615 `msduseroffice` rows for 253 users in scope | **255** users updated; Admin = "1, 2, 3, 4, 5, 6"; the 55 users left NULL belong only to test offices 1 (50) / 5 (5) with no access to 2/3/4/6 |

---

## 7. Notes

- **Tax1/2/3 are not visible to saksham in our form or view** — `pan` / `gst_no` fields are gated to altico / lisha. The values live in the columns and, for the user, in **Remarks**, which is why the exe appends them there.
- **Reminder Message** (`contact.custom1`) has no field on our form or view, so the exe also appends it to **Remarks** (same as Tax1/2/3).
- **Area:** editable on the saksham form, but the view shows it for JMCA only.
- **Phone semantics:** `vtel1` is almost always a landline, so it stays in Landline; Mobile is filled only from `vmobile`.
- **Person branch:** eBizWiz has no branch per person; every person gets the contact's install branch so the Branch dropdown is pre-selected.
- **Sales / Service person** are matched to `users` by name; the Contact module first creates every source user, so all resolve.
- `contact.owner` = migration user (Admin) on every row, same as every other module.
- **User branch access.** Sales Invoice, GRN, MRO and GTA lists and views filter on `branchcode in (users.companybranch)`; a user without branches 3–6 there sees none of the migrated documents of those branches. The exe fills it from the client's own user–office access (`msduseroffice`, 615 rows for 253 users in scope).
- The exe writes unmatched cities to `C:\FTFS\Maping\unmatched_cities.txt`; nothing is written when every city matches.

