# 01 — Contact

| | |
|---|---|
| **Client screen** | eBizWiz → `Master > Party` (Party Master + Party Detail) |
| **Our screen** | EdifyBiz → `Contact` |
| **Source tables** | `mstparty` (header), `msdparty` (contact persons) |
| **Target tables** | `contact`, `mltaddress`, `mltcontact` |
| **Code** | `Program.cs` → `static class Contact` |

One Party Master row becomes one `contact` row, one or two `mltaddress` rows, and one `mltcontact` row per contact person.

---

## 1. Migrated

### 1.1 Party Master → `contact`  (main form; Remark and Licenses & Statutories are tabs)

| # | Client UI | Client table.column | Our UI | Our table.column | Note |
|---|---|---|---|---|---|
| 1 | Title + Name | `mstparty.ntitle` + `vname` | Name | `contact.name` | Title prefixed, capped 150 |
| 2 | Party Type | `mstparty.npartytype` → `mstpartytype.vname` | Party Type | `contact.ctype` | Name stored, not code |
| 3 | Party Profile | `mstparty.npartyprofile` → `mstpartyprofile.vname` | Party Profile | `contact.companygroup` | Name stored, not code |
| 4 | Sales Person | `mstparty.nusersales` → `mstusers.vname` | Sales Person | `contact.crmagent` | Matched to the migrated user by name |
| 5 | Service Person | `mstparty.nuserservice` → `mstusers.vname` | Service Person | `contact.fieldagent` | Matched to the migrated user by name |
| 6 | Active | `mstparty.bactive` | Active | `contact.isdeleted` | Inverted |
| 7 | (record office) | `mstparty.nofficeid` | Business Division Access | `contact.companybranch` | Office → `companyaddress` branch |
| 8 | Tax1 | `mstparty.vtax1` | Licenses & Statutories tab → PAN | `contact.pan` | Capped 50, also pattern-parsed to the address |
| 9 | Tax2 | `mstparty.vtax2` | Licenses & Statutories tab → GST NO. | `contact.gst_no` | Capped 50, also pattern-parsed to the address |
| 10 | Tax3 | `mstparty.vtax3` | Licenses & Statutories tab → CST / Aadhar | `contact.cst` | Capped 50, also pattern-parsed to the address |
| 11 | Remarks | `mstparty.vremarks` | Remark tab → Remark | `contact.remark` | |
| 12 | Reminder Message | `mstparty.vremindermessage` | — | `contact.custom1` | Not shown on any screen |
| 13 | (audit) | `addedon` / `addedby` | Created On / By | `contact.createdon` / `createdby` | |
| 14 | (audit) | `editedon` / `editedby` | Updated On / By | `contact.updatedon` / `updatedby` | Falls back to added on / by |

`contact.owner` is the migration user, not the salesperson, because ownership decides visibility in EdifyBiz.

### 1.2 Install address → `mltaddress`  (Additional Addresses tab; `isdefault=1`, `isshipto=1`)

| # | Client UI | Client table.column | Our UI | Our table.column | Note |
|---|---|---|---|---|---|
| 1 | Address | `mstparty.vinstaddress` | Address | `mltaddress.address` | |
| 2 | Postal Code | `mstparty.vinstpostalcode` | Pincode | `mltaddress.pincode` | |
| 3 | Telephone 1 | `mstparty.vinsttel1` | Landline | `mltaddress.telephone1` | |
| 4 | Telephone 2 | `mstparty.vinsttel2` | Landline 2 | `mltaddress.telephone2` | |
| 5 | Fax | `mstparty.vinstfax` | Fax | `mltaddress.fax1` | |
| 6 | Email | `mstparty.vinstemail` | Email | `mltaddress.email1` | |
| 7 | Website | `mstparty.vwebsite` | Website | `mltaddress.website1` | Header field in eBizWiz, address field here |
| 8 | City | `mstparty.ncity` → `mstcity.vname` | City | `mltaddress.citycode` | Matched by name; missing city / state / country created |
| 9 | Route (Area) | `mstparty.nroute` → `mstroute.vname` | Area | `mltaddress.area` | |
| 10 | — | derived: city, else route, else office name, else `Main Office` | Place / Branch / Office | `mltaddress.place` | eBizWiz has no place field |
| 11 | Tax1 / 2 / 3 | parsed out of `vtax1..3` | GSTIN | `mltaddress.gstno` | Only if a valid GSTIN is found in the text |
| 12 | Tax1 / 2 / 3 | parsed out of `vtax1..3` | PAN | `mltaddress.pan` | Only if a valid PAN is found |
| 13 | Tax1 / 2 / 3 | parsed out of `vtax1..3` | Import / Export Code | `mltaddress.impexpCode` | Only if an IEC is found |
| 14 | Tax1 / 2 / 3 | parsed out of `vtax1..3` | VAT | `mltaddress.vat` | Only if a VAT / TIN is found |

Written when the party has an install address, route, telephone or city.

### 1.3 Billing address → `mltaddress`  (Additional Addresses tab; `isbillto=1`)

Second address row, only when the billing address is filled, differs from the install address, and does not read "same as above".

| # | Client UI | Client table.column | Our UI | Our table.column |
|---|---|---|---|---|
| 1 | Billing Address | `mstparty.vbilladdress` | Address | `mltaddress.address` |
| 2 | Postal Code | `mstparty.vbillpostalcode` | Pincode | `mltaddress.pincode` |
| 3 | Telephone 1 | `mstparty.vbilltel1` | Landline | `mltaddress.telephone1` |
| 4 | Telephone 2 | `mstparty.vbilltel2` | Landline 2 | `mltaddress.telephone2` |
| 5 | Fax | `mstparty.vbillfax` | Fax | `mltaddress.fax1` |
| 6 | Email | `mstparty.vbillemail` | Email | `mltaddress.email1` |
| 7 | City | `mstparty.ncity` | City | `mltaddress.citycode` |

### 1.4 Party Detail → `mltcontact`  (Persons tab)

One row per contact person. Blank name is skipped. The first person is marked default and every person inherits the party's default address branch.

| # | Client UI | Client table.column | Our UI | Our table.column | Note |
|---|---|---|---|---|---|
| 1 | Title + Contact Person | `msdparty.ntitle` + `vcontactperson` | Name | `mltcontact.name` | Title prefixed, capped 100 |
| 2 | Designation | `msdparty.ndesignation`, else `vdesignation` | Designation | `mltcontact.designation` | Seeded into `desigmaster` |
| 3 | Mobile | `msdparty.vmobile` | Mobile | `mltcontact.mobile1` | |
| 4 | Telephone 1 | `msdparty.vtel1` | Landline | `mltcontact.telephone1` | Kept as landline, not promoted to mobile |
| 5 | Telephone 2 | `msdparty.vtel2` | Landline 2 | `mltcontact.telephone2` | |
| 6 | Email | `msdparty.vemail` | Email | `mltcontact.email1` | |
| 7 | Communication Model | `msdparty.ncommunication` → `mstfixedselection.vdisplayvalue` | Preferred mode of Communication | `mltcontact.communication` | Seeded into `miscellaneous(Contact / Communication)` |
| 8 | Date of Birth | `msdparty.ddob` | Birthday | `mltcontact.bday` | |
| 9 | Anniversary Date | `msdparty.danniversary` | Anniversary | `mltcontact.anniversary` | |
| 10 | Date of Birth (spouse) | `msdparty.dspousedob` | Spouse's Birthday | `mltcontact.spousebday` | |
| 11 | Date of Birth (child 1) | `msdparty.dchild1dob` | 1st Child's Birthday | `mltcontact.firstchildbday` | |
| 12 | Date of Birth (child 2) | `msdparty.dchild2dob` | 2nd Child's Birthday | `mltcontact.secondchildbday` | |
| 13 | Remarks | `msdparty.vremarks` | Remark | `mltcontact.otherinfo` | |

### 1.5 Masters seeded

| Source | Target |
|---|---|
| `mstcity` + its state / country | `city`, `state`, `country` |
| `mstpartytype` | `miscellaneous(Contact / Party Type)` |
| `mstpartyprofile` | `miscellaneous(Contact / Party Profile)` |
| `mstdesignation` + `msdparty.ndesignation` | `desigmaster` |
| `msdparty.ncommunication` | `miscellaneous(Contact / Communication)` |
| `mstusers` | `users` |
| `mstoffice` | `companyaddress` + each user's branch access |

---

## 2. Folded into a text field

| # | Client UI | Client table.column | Lands in | Written as |
|---|---|---|---|---|
| 1 | Reminder Message | `mstparty.vremindermessage` | `contact.remark` | `Reminder Message: <value>` |
| 2 | Tax1 | `mstparty.vtax1` | `contact.remark` | `Tax1: <value>` — only when no GSTIN / PAN / IEC / VAT was recognised in it |
| 3 | Tax2 | `mstparty.vtax2` | `contact.remark` | `Tax2: <value>` — same condition |
| 4 | Tax3 | `mstparty.vtax3` | `contact.remark` | `Tax3: <value>` — same condition |
| 5 | Spouse Name | `msdparty.vspousename` | `mltcontact.otherinfo` | `Spouse: <value>` |

---

## 3. Skipped

### 3.1 `mstparty`

| # | Client table.column | On client screen | Why |
|---|---|---|---|
| 1 | `nunder` | no | Parent-party hierarchy, no EdifyBiz equivalent |
| 2 | `nnoofmachines` | no | Derived data |
| 3 | `npartyprofile1`, `npartyprofile2` | no | Secondary profiles; EdifyBiz holds one |
| 4 | `ikey` | no | Internal key |
| 5 | `vlatlong`, `mlatlong` | no | Mobile-app map coordinates |
| 6 | `maddedby`, `maddedon`, `meditedby`, `meditedon` | no | Mobile-app audit; web audit used instead |
| 7 | `ipaddress` | no | Audit only |
| 8 | `VERPCODE`, `acode` | no | External ERP keys |

### 3.2 `msdparty`

| # | Client table.column | On client screen | Why |
|---|---|---|---|
| 1 | `vchild1`, `vchild2` | yes — Child 1 / 2 Name | EdifyBiz keeps the birthdays only, no name field |
| 2 | `btpc` | yes — TPC | No EdifyBiz equivalent |
| 3 | `bactive` | yes — Active | No person-level active flag; inactive persons migrate too |
| 4 | `vaddress` | no | Person-level address; EdifyBiz addresses are party-level |
| 5 | `npartyprofile1` … `npartyprofile4` | no | Person-level profiles, no equivalent |
| 6 | `addedby`, `addedon`, `editedby`, `editedon` | no | Person-level audit; the party's values are used |
| 7 | `ipaddress`, `acode` | no | Audit / external key |
