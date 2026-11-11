# Project context and accepted constraints

- The baseline is `BA_Nikitsenkau_Aliaksei 2.docx`. Check `docs/ba-traceability.md` before changing domain behaviour; do not claim that all BA Must stories or KPI/NFR targets are complete.
- The owner explicitly deferred all publication. Do not push, publish a site, create a remote repository or grant an expert access without later explicit authorization. Local artifacts and local Git bundles are allowed.
- Preserve the database assessment criteria in `docs/assessment-requirements.md` and the prepared database materials in `docs/expert-checklist.md`. The web runtime must use a restricted PostgreSQL login, never the administrator or migration owner. Do not commit credentials, Data Protection keys, pgdata or `.local`.
- The root `.git` is read-only in the current agent environment. `scripts/Save-LocalReviewBundle.ps1` prepares local Git metadata in `.local/review-git` and a cloneable bundle without writing to the protected root metadata.
- UX assessment criteria and evidence are in `docs/assessment-requirements.md` and `docs/ux/README.md`. Keep PDF, editable source, prototype and evidence aligned with the BA and implemented UI.
- Do not fabricate interviews, participant data or usability outcomes. BA contains one exploratory written response P1; informal discussions are not completed interviews. Proto-personas are hypotheses until validated. Automated browser checks are not human usability testing.
- Preserve the five documented unused fragments; the owner asked to document rather than delete them. They are listed in `docs/unused-code-audit.md`.
- For changes, run appropriate checks and verify affected user flows in the browser. Use clearly labelled synthetic accounts/records; retain privacy and moderation boundaries.
