const { themes } = require('prism-react-renderer');

const lightCodeTheme = themes.github;
const darkCodeTheme = themes.dracula;

module.exports = {
  title: 'Aonik Docs',
  tagline: 'Documentation for the Aonik platform',
  url: 'https://michael.josiah.github.io',
  baseUrl: '/aonik/',
  onBrokenLinks: 'throw',
  onBrokenMarkdownLinks: 'warn',
  organizationName: 'michaeljosiah',
  projectName: 'aonik',
  deploymentBranch: 'gh-pages',
  trailingSlash: false,
  presets: [
    [
      'classic',
      {
        docs: {
          path: 'docs',
          routeBasePath: '/',
          sidebarPath: require.resolve('./sidebars.js'),
          editUrl: 'https://github.com/michaeljosiah/aonik/edit/main/apps/docs-site/',
          docItemComponent: '@theme/ApiItem',
        },
        blog: false,
        theme: {
          customCss: require.resolve('./src/css/custom.css'),
        },
      },
    ],
  ],
  themes: ['docusaurus-theme-openapi-docs'],
  plugins: [
    [
      'docusaurus-plugin-openapi-docs',
      {
        id: 'api',
        docsPluginId: 'classic',
        config: {
          aonik: {
            specPath: 'openapi/aonik-api.yaml',
            outputDir: 'docs/api',
            sidebarOptions: {
              groupPathsBy: 'tag',
              categoryLinkSource: 'tag',
            },
          },
        },
      },
    ],
    function polyfillPlugin() {
      return {
        name: 'node-polyfill-plugin',
        configureWebpack() {
          return {
            resolve: {
              fallback: {
                path: require.resolve('path-browserify'),
              },
            },
          };
        },
      };
    },
  ],
  themeConfig: {
    navbar: {
      title: 'Aonik',
      items: [
        { to: '/', label: 'Docs', position: 'left' },
        { to: '/api/aonik-api', label: 'API', position: 'left' },
        {
          href: 'https://github.com/michaeljosiah/aonik',
          label: 'GitHub',
          position: 'right',
        },
      ],
    },
    footer: {
      style: 'dark',
      links: [
        {
          title: 'Docs',
          items: [
            { label: 'Getting Started', to: '/guides/getting-started' },
          ],
        },
        {
          title: 'API',
          items: [
            { label: 'API Reference', to: '/api/aonik-api' },
          ],
        },
        {
          title: 'Community',
          items: [
            { label: 'GitHub', href: 'https://github.com/michaeljosiah/aonik' },
          ],
        },
      ],
      copyright: `Copyright © ${new Date().getFullYear()} Aonik`,
    },
    prism: {
      theme: lightCodeTheme,
      darkTheme: darkCodeTheme,
    },
  },
};
