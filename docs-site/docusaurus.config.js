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
          editUrl: 'https://github.com/michaeljosiah/aonik/edit/main/docs-site/',
        },
        blog: false,
        theme: {
          customCss: require.resolve('./src/css/custom.css'),
        },
      },
    ],
  ],
  themeConfig: {
    navbar: {
      title: 'Aonik',
      items: [
        { to: '/', label: 'Docs', position: 'left' },
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
