---
title: ADR 003 - Relational Database Selection
weight: 3
---

<!--
Sample of a short, early-style ADR that has since been superseded. Demonstrates the Superseded
status line linking to the replacing ADR, and that superseded ADRs keep their original content
otherwise untouched. All content is invented for illustration. A real ADR does not include this
comment.
-->

- **Date:** 2025-02-10
- **Status:** [Superseded](/architecture/adrs/adr-012-database-migration/)

## Context

The application needs a primary datastore before the first feature can persist anything. The team
has operational experience with MySQL and PostgreSQL; the hosting platform offers managed versions
of both.

## Problem Statement

Choose a relational database that the team can operate confidently and that the hosting platform
supports as a managed service.

## Decision

Adopt MySQL 8 as the primary datastore.

## Options Considered

- MySQL 8
- PostgreSQL 16
- SQL Server

## Rationale

- The team's deepest operational experience is with MySQL.
- The managed MySQL offering was generally available; managed PostgreSQL was in preview at the
  time.

## Consequences

### Positive

- No operational learning curve for the initial launch.

### Negative

- Features later found necessary (listen/notify, richer JSON indexing) are weaker in MySQL and
  eventually motivated the migration recorded in the superseding ADR.
