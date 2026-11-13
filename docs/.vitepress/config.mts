import { defineConfig } from "vitepress";

// Served at https://func.algo.xyz (see public/CNAME), so no base path.
export default defineConfig({
  // "FUNC Docs" in browser tabs, so they aren't mistaken for the dashboard's
  // "FUNC" tab; the nav bar still says FUNC (siteTitle below).
  title: "FUNC Docs",
  description:
    "Funk's Ultimate Node Controller: run an Algorand node and participate in consensus, without the command line.",
  lang: "en-US",
  cleanUrls: true,
  lastUpdated: true,
  // The app itself, on the reader's own computer.
  ignoreDeadLinks: [/^https?:\/\/localhost/],
  head: [
    // Its own tab icon too, distinct from the dashboard's.
    ["link", { rel: "icon", type: "image/svg+xml", href: "/favicon-docs.svg" }],
  ],
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
