import React from 'react';
import ComponentCreator from '@docusaurus/ComponentCreator';

export default [
  {
    path: '/aonik/__docusaurus/debug',
    component: ComponentCreator('/aonik/__docusaurus/debug', '775'),
    exact: true
  },
  {
    path: '/aonik/__docusaurus/debug/config',
    component: ComponentCreator('/aonik/__docusaurus/debug/config', 'da4'),
    exact: true
  },
  {
    path: '/aonik/__docusaurus/debug/content',
    component: ComponentCreator('/aonik/__docusaurus/debug/content', '013'),
    exact: true
  },
  {
    path: '/aonik/__docusaurus/debug/globalData',
    component: ComponentCreator('/aonik/__docusaurus/debug/globalData', '7af'),
    exact: true
  },
  {
    path: '/aonik/__docusaurus/debug/metadata',
    component: ComponentCreator('/aonik/__docusaurus/debug/metadata', '69e'),
    exact: true
  },
  {
    path: '/aonik/__docusaurus/debug/registry',
    component: ComponentCreator('/aonik/__docusaurus/debug/registry', '748'),
    exact: true
  },
  {
    path: '/aonik/__docusaurus/debug/routes',
    component: ComponentCreator('/aonik/__docusaurus/debug/routes', '159'),
    exact: true
  },
  {
    path: '/aonik/',
    component: ComponentCreator('/aonik/', '1e2'),
    routes: [
      {
        path: '/aonik/',
        component: ComponentCreator('/aonik/', '628'),
        routes: [
          {
            path: '/aonik/',
            component: ComponentCreator('/aonik/', '111'),
            routes: [
              {
                path: '/aonik/Architecture',
                component: ComponentCreator('/aonik/Architecture', '6a5'),
                exact: true,
                sidebar: "docs"
              },
              {
                path: '/aonik/architecture/clean-architecture',
                component: ComponentCreator('/aonik/architecture/clean-architecture', '837'),
                exact: true,
                sidebar: "docs"
              },
              {
                path: '/aonik/architecture/data-flow',
                component: ComponentCreator('/aonik/architecture/data-flow', 'c79'),
                exact: true,
                sidebar: "docs"
              },
              {
                path: '/aonik/architecture/module-organization',
                component: ComponentCreator('/aonik/architecture/module-organization', '126'),
                exact: true,
                sidebar: "docs"
              },
              {
                path: '/aonik/architecture/overview',
                component: ComponentCreator('/aonik/architecture/overview', '60c'),
                exact: true,
                sidebar: "docs"
              },
              {
                path: '/aonik/architecture/technology-stack',
                component: ComponentCreator('/aonik/architecture/technology-stack', '462'),
                exact: true,
                sidebar: "docs"
              },
              {
                path: '/aonik/contributing/code-style',
                component: ComponentCreator('/aonik/contributing/code-style', '882'),
                exact: true,
                sidebar: "docs"
              },
              {
                path: '/aonik/contributing/git-workflow',
                component: ComponentCreator('/aonik/contributing/git-workflow', 'cbd'),
                exact: true,
                sidebar: "docs"
              },
              {
                path: '/aonik/contributing/pull-requests',
                component: ComponentCreator('/aonik/contributing/pull-requests', 'ff7'),
                exact: true,
                sidebar: "docs"
              },
              {
                path: '/aonik/database/entity-relationships',
                component: ComponentCreator('/aonik/database/entity-relationships', '3fa'),
                exact: true,
                sidebar: "docs"
              },
              {
                path: '/aonik/database/schema-overview',
                component: ComponentCreator('/aonik/database/schema-overview', '075'),
                exact: true,
                sidebar: "docs"
              },
              {
                path: '/aonik/database/tenant-isolation',
                component: ComponentCreator('/aonik/database/tenant-isolation', '8ec'),
                exact: true,
                sidebar: "docs"
              },
              {
                path: '/aonik/DBCONTEXT-IMPROVEMENTS',
                component: ComponentCreator('/aonik/DBCONTEXT-IMPROVEMENTS', '496'),
                exact: true
              },
              {
                path: '/aonik/decisions',
                component: ComponentCreator('/aonik/decisions', '0be'),
                exact: true,
                sidebar: "docs"
              },
              {
                path: '/aonik/decisions/anemic-domain-model',
                component: ComponentCreator('/aonik/decisions/anemic-domain-model', '2ec'),
                exact: true,
                sidebar: "docs"
              },
              {
                path: '/aonik/decisions/custom-ai-implementation-vs-maf',
                component: ComponentCreator('/aonik/decisions/custom-ai-implementation-vs-maf', '4a0'),
                exact: true,
                sidebar: "docs"
              },
              {
                path: '/aonik/decisions/no-generic-repository',
                component: ComponentCreator('/aonik/decisions/no-generic-repository', '9c7'),
                exact: true,
                sidebar: "docs"
              },
              {
                path: '/aonik/deployment/azure-deployment',
                component: ComponentCreator('/aonik/deployment/azure-deployment', '587'),
                exact: true,
                sidebar: "docs"
              },
              {
                path: '/aonik/deployment/docker',
                component: ComponentCreator('/aonik/deployment/docker', '3c7'),
                exact: true,
                sidebar: "docs"
              },
              {
                path: '/aonik/deployment/local-development',
                component: ComponentCreator('/aonik/deployment/local-development', 'f14'),
                exact: true,
                sidebar: "docs"
              },
              {
                path: '/aonik/features/ai-integration',
                component: ComponentCreator('/aonik/features/ai-integration', '3db'),
                exact: true,
                sidebar: "docs"
              },
              {
                path: '/aonik/features/authentication-authorization',
                component: ComponentCreator('/aonik/features/authentication-authorization', '8b5'),
                exact: true,
                sidebar: "docs"
              },
              {
                path: '/aonik/features/billing',
                component: ComponentCreator('/aonik/features/billing', 'a4a'),
                exact: true,
                sidebar: "docs"
              },
              {
                path: '/aonik/features/ledger',
                component: ComponentCreator('/aonik/features/ledger', '1f2'),
                exact: true,
                sidebar: "docs"
              },
              {
                path: '/aonik/features/payments',
                component: ComponentCreator('/aonik/features/payments', '5e8'),
                exact: true,
                sidebar: "docs"
              },
              {
                path: '/aonik/features/tenant-management',
                component: ComponentCreator('/aonik/features/tenant-management', 'f65'),
                exact: true,
                sidebar: "docs"
              },
              {
                path: '/aonik/guides/api-endpoints',
                component: ComponentCreator('/aonik/guides/api-endpoints', '75b'),
                exact: true,
                sidebar: "docs"
              },
              {
                path: '/aonik/guides/application-services',
                component: ComponentCreator('/aonik/guides/application-services', '187'),
                exact: true,
                sidebar: "docs"
              },
              {
                path: '/aonik/guides/authentication-auth0',
                component: ComponentCreator('/aonik/guides/authentication-auth0', 'a9f'),
                exact: true,
                sidebar: "docs"
              },
              {
                path: '/aonik/guides/authentication-azure-ad',
                component: ComponentCreator('/aonik/guides/authentication-azure-ad', 'f31'),
                exact: true,
                sidebar: "docs"
              },
              {
                path: '/aonik/guides/authentication-troubleshooting',
                component: ComponentCreator('/aonik/guides/authentication-troubleshooting', 'd70'),
                exact: true,
                sidebar: "docs"
              },
              {
                path: '/aonik/guides/database-migrations',
                component: ComponentCreator('/aonik/guides/database-migrations', 'ee1'),
                exact: true,
                sidebar: "docs"
              },
              {
                path: '/aonik/guides/domain-entities',
                component: ComponentCreator('/aonik/guides/domain-entities', 'c89'),
                exact: true,
                sidebar: "docs"
              },
              {
                path: '/aonik/guides/getting-started',
                component: ComponentCreator('/aonik/guides/getting-started', 'd8e'),
                exact: true,
                sidebar: "docs"
              },
              {
                path: '/aonik/guides/roles-and-permissions',
                component: ComponentCreator('/aonik/guides/roles-and-permissions', '40e'),
                exact: true,
                sidebar: "docs"
              },
              {
                path: '/aonik/guides/testing',
                component: ComponentCreator('/aonik/guides/testing', '750'),
                exact: true,
                sidebar: "docs"
              },
              {
                path: '/aonik/patterns/dto-mapping',
                component: ComponentCreator('/aonik/patterns/dto-mapping', 'a3a'),
                exact: true,
                sidebar: "docs"
              },
              {
                path: '/aonik/patterns/error-handling',
                component: ComponentCreator('/aonik/patterns/error-handling', '6c4'),
                exact: true,
                sidebar: "docs"
              },
              {
                path: '/aonik/patterns/service-layer',
                component: ComponentCreator('/aonik/patterns/service-layer', '1b3'),
                exact: true,
                sidebar: "docs"
              },
              {
                path: '/aonik/patterns/validation',
                component: ComponentCreator('/aonik/patterns/validation', '48a'),
                exact: true,
                sidebar: "docs"
              },
              {
                path: '/aonik/reference/permissions',
                component: ComponentCreator('/aonik/reference/permissions', '9e3'),
                exact: true,
                sidebar: "docs"
              },
              {
                path: '/aonik/requirements/user-onboarding-specification',
                component: ComponentCreator('/aonik/requirements/user-onboarding-specification', '078'),
                exact: true,
                sidebar: "docs"
              },
              {
                path: '/aonik/SwaggerAuthentication',
                component: ComponentCreator('/aonik/SwaggerAuthentication', '8a8'),
                exact: true
              },
              {
                path: '/aonik/Testing',
                component: ComponentCreator('/aonik/Testing', '909'),
                exact: true,
                sidebar: "docs"
              },
              {
                path: '/aonik/Troubleshooting',
                component: ComponentCreator('/aonik/Troubleshooting', '938'),
                exact: true,
                sidebar: "docs"
              },
              {
                path: '/aonik/',
                component: ComponentCreator('/aonik/', 'f6f'),
                exact: true,
                sidebar: "docs"
              },
              {
                path: '/aonik/',
                component: ComponentCreator('/aonik/', 'b57'),
                exact: true
              }
            ]
          }
        ]
      }
    ]
  },
  {
    path: '*',
    component: ComponentCreator('*'),
  },
];
