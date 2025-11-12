/**
 * Trigger Bulk Triage Script
 * 
 * This script finds or creates a persistent bulk triage tracker issue
 * and posts a comment to trigger @copilot for bulk triage.
 */

const fs = require('fs');
const path = require('path');

module.exports = async ({github, context, core}) => {

  const today = new Date().toISOString().split('T')[0];
  const trackerTitle = '[Triage] Bulk Triage Tracker';

  // Read the issue template
  const templatePath = path.join(
    process.env.GITHUB_WORKSPACE,
    '.github/ISSUE_TEMPLATE/triage.md'
  );
  
  let templateBody = '';
  try {
    const templateContent = fs.readFileSync(templatePath, 'utf8');
    // Extract content after the front matter (---...---)
    const parts = templateContent.split('---');
    if (parts.length >= 3) {
      templateBody = parts.slice(2).join('---').trim();
    } else {
      // Fallback if no front matter
      templateBody = templateContent;
    }
  } catch (error) {
    core.warning(`Could not read template file: ${error.message}`);
    // Use fallback content
    templateBody = `## Bulk Triage Request

@copilot **MUST** follow \`.team/duties/TRIAGE_DUTY.md\` in **BULK MODE**.

**See**: \`.team/duties/TRIAGE_DUTY.md\` for the complete bulk triage process.`;
  }

  // Find or create the bulk triage tracker issue
  const existingIssues = await github.rest.issues.listForRepo({
    owner: context.repo.owner,
    repo: context.repo.repo,
    state: 'open',
    labels: ['workflow:triage'],
    per_page: 100
  });

  let trackerIssue = existingIssues.data.find(
    issue => issue.title === trackerTitle
  );

  // Create tracker issue if it doesn't exist
  if (!trackerIssue) {
    console.log('Creating bulk triage tracker issue...');
    
    // Use template content for the tracker issue body
    const trackerBody = `## Bulk Triage Tracker

This is a persistent issue for triggering bulk triage.

**⚠️ IMPORTANT**: This issue must be assigned to @copilot for automated triage to work.

---

${templateBody}

---

**Status**: Ready for triage requests`;

    const created = await github.rest.issues.create({
      owner: context.repo.owner,
      repo: context.repo.repo,
      title: trackerTitle,
      body: trackerBody,
      labels: ['workflow:triage']
    });
    
    trackerIssue = created.data;
    console.log(`Created tracker issue #${trackerIssue.number}`);
  }

  // Get count of issues in triage queue
  const triageIssues = await github.rest.issues.listForRepo({
    owner: context.repo.owner,
    repo: context.repo.repo,
    state: 'open',
    labels: ['workflow:triage'],
    per_page: 100
  });

  // Filter out the tracker issue itself
  const queueSize = triageIssues.data.filter(
    issue => issue.title !== trackerTitle
  ).length;

  // Add comment to trigger @copilot
  const comment = `## Bulk Triage Request - ${today}

@copilot Please perform bulk triage following \`.team/duties/TRIAGE_DUTY.md\` in **BULK MODE**.

**Queue Size**: ${queueSize} issue(s) awaiting triage

**Instructions**: Process all issues in the triage queue and route them to the appropriate workflow duties.`;

  await github.rest.issues.createComment({
    owner: context.repo.owner,
    repo: context.repo.repo,
    issue_number: trackerIssue.number,
    body: comment
  });

  console.log(`Posted triage request to issue #${trackerIssue.number}`);
  console.log(`Queue size: ${queueSize} issue(s)`);
};
