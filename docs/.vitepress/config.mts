import { defineConfig } from "vitepress";

// Served at https://func.algo.xyz (see public/CNAME), so no base path.
const SITE_URL = "https://func.algo.xyz";
const DESCRIPTION =
  "Funk's Ultimate Node Controller: run an Algorand node and participate in consensus, without the command line.";

export default defineConfig({
  // "FUNC Docs" in browser tabs, so they aren't mistaken for the dashboard's
  // "FUNC" tab; the nav bar still says FUNC (siteTitle below).
  title: "FUNC Docs",
  description: DESCRIPTION,
  lang: "en-US",
  cleanUrls: true,
  lastUpdated: true,
  // The app itself, on the reader's own computer.
  ignoreDeadLinks: [/^https?:\/\/localhost/],
  head: [
    // Its own tab icon too, distinct from the dashboard's.
    ["link", { rel: "icon", type: "image/svg+xml", href: "/favicon-docs.svg" }],
    ["meta", { name: "theme-color", content: "#1acbf7" }],
    // Link previews (Reddit, Bluesky, Discord, Slack, X...). The image is
    // 1200x630 and must be an absolute URL. Per-page title, description and
    // URL are added in transformHead below.
    ["meta", { property: "og:type", content: "website" }],
    ["meta", { property: "og:site_name", content: "FUNC" }],
    ["meta", { property: "og:locale", content: "en_US" }],
    ["meta", { property: "og:image", content: `${SITE_URL}/og-image.png` }],
    ["meta", { property: "og:image:type", content: "image/png" }],
    ["meta", { property: "og:image:width", content: "1200" }],
    ["meta", { property: "og:image:height", content: "630" }],
    [
      "meta",
      { property: "og:image:alt", content: "FUNC logo beside the FUNC dashboard" },
    ],
    ["meta", { name: "twitter:card", content: "summary_large_image" }],
    ["meta", { name: "twitter:image", content: `${SITE_URL}/og-image.png` }],
  ],
  sitemap: { hostname: SITE_URL },
  transformHead({ pageData, title, description }) {
    const path = pageData.relativePath
      .replace(/(^|\/)index\.md$/, "$1")
      .replace(/\.md$/, "");
    const url = `${SITE_URL}/${path}`;
    // The home page reads better as the product name than "FUNC Docs".
    const ogTitle =
      pageData.frontmatter.layout === "home"
        ? "FUNC: Funk's Ultimate Node Controller"
        : title;
    const desc = description || DESCRIPTION;
    return [
      ["link", { rel: "canonical", href: url }],
      ["meta", { property: "og:url", content: url }],
      ["meta", { property: "og:title", content: ogTitle }],
      ["meta", { property: "og:description", content: desc }],
      ["meta", { name: "twitter:title", content: ogTitle }],
      ["meta", { name: "twitter:description", content: desc }],
    ];
  },
  themeConfig: {
    siteTitle: "FUNC",
    logo: "/icon.svg",
    nav: [
      { text: "Install", link: "/install" },
      { text: "Getting Started", link: "/getting-started" },
      {
        text: "Releases",
        link: "https://github.com/GalaxyPay/func/releases",
      },
    ],
    sidebar: [
      {
        text: "Start Here",
        items: [
          { text: "Install", link: "/install" },
          { text: "Getting Started", link: "/getting-started" },
          { text: "Participating in Consensus", link: "/consensus" },
        ],
      },
      {
        text: "Using FUNC",
        items: [
          { text: "The Dashboard", link: "/dashboard" },
          { text: "Settings", link: "/settings" },
          { text: "Manage Menu", link: "/manage" },
          { text: "Keeping Your Node Running", link: "/running" },
        ],
      },
      {
        text: "Advanced",
        items: [
          { text: "Password", link: "/password" },
          { text: "Remote Access", link: "/remote-access" },
          { text: "Existing Nodes (Mac / Linux)", link: "/existing-nodes" },
          { text: "Uninstall", link: "/uninstall" },
        ],
      },
    ],
    socialLinks: [
      { icon: "github", link: "https://github.com/GalaxyPay/func" },
    ],
    search: { provider: "local" },
    editLink: {
      pattern: "https://github.com/GalaxyPay/func/edit/main/docs/:path",
      text: "Suggest a change to this page",
    },
    footer: {
      message: "FUNC is open source.",
    },
  },
});
