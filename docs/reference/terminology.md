# Terminology

This document defines the terminology used across the platform for catalog, pricing, and partner capability modeling.

## CapabilityType

The product domain a partner supports. This is broad and high level.

Examples:
- BillPayment
- Remittance
- Payouts
- Collections

CapabilityType prefixes (aligned to legacy MTM):
- BANKTRANSFER
- CASHPAYMENT
- MOBILETOPUP
- BILLPAY
- FXTRADE
- PRODUCTSALE
- MOBILEWALLET
- CARDPAYMENT

## CategoryCode

The catalog grouping used for discovery and UI organization. Categories group billers and services within a domain.

Examples:
- Electricity
- Water
- Telecom
- TV
- Government

## ServiceCode

The stable identifier for a specific payable service. It is used for pricing policy matching, routing, and quote persistence.

Examples:
- BILLPAY.ELECTRICITY.PREPAID
- BILLPAY.ELECTRICITY.POSTPAID
- BILLPAY.WATER.POSTPAID
- BILLPAY.TELECOM.AIRTIME

## capabilityType (API filter)

`capabilityType` on `/catalog/countries` is matched as a ServiceCode prefix. Use the same prefix you define in ServiceCode (for example `BILLPAY`).

## Recommended Flow

1. Catalog defines CategoryCode and ServiceCode per service.
2. Pricing requests and quotes always use ServiceCode.
3. Orders store ServiceCode on each item to keep intent and pricing aligned.
