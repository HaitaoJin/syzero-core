import { defineConfig } from "vitepress";

const moduleLinks = [
  { text: "SyZero", link: "/modules/SyZero" },
  { text: "SyZero.ApiGateway", link: "/modules/SyZero.ApiGateway" },
  { text: "SyZero.AspNetCore", link: "/modules/SyZero.AspNetCore" },
  { text: "SyZero.AspNetCore.SpaProxy", link: "/modules/SyZero.AspNetCore.SpaProxy" },
  { text: "SyZero.AutoMapper", link: "/modules/SyZero.AutoMapper" },
  { text: "SyZero.Consul", link: "/modules/SyZero.Consul" },
  { text: "SyZero.DynamicGrpc", link: "/modules/SyZero.DynamicGrpc" },
  { text: "SyZero.DynamicWebApi", link: "/modules/SyZero.DynamicWebApi" },
  { text: "SyZero.EntityFrameworkCore", link: "/modules/SyZero.EntityFrameworkCore" },
  { text: "SyZero.Feign", link: "/modules/SyZero.Feign" },
  { text: "SyZero.Log4Net", link: "/modules/SyZero.Log4Net" },
  { text: "SyZero.MongoDB", link: "/modules/SyZero.MongoDB" },
  { text: "SyZero.Nacos", link: "/modules/SyZero.Nacos" },
  { text: "SyZero.OpenTelemetry", link: "/modules/SyZero.OpenTelemetry" },
  { text: "SyZero.RabbitMQ", link: "/modules/SyZero.RabbitMQ" },
  { text: "SyZero.Redis", link: "/modules/SyZero.Redis" },
  { text: "SyZero.SqlSugar", link: "/modules/SyZero.SqlSugar" },
  { text: "SyZero.Swagger", link: "/modules/SyZero.Swagger" },
  { text: "SyZero.Web.Common", link: "/modules/SyZero.Web.Common" }
];

export default defineConfig({
  base: "/syzero/",
  title: "SyZero",
  description: "轻量级、模块化 .NET 微服务开发框架文档",
  head: [
    ["link", { rel: "icon", href: "/syzero/icon/logo.png" }],
    ["link", { rel: "apple-touch-icon", href: "/syzero/icon/logo.png" }],
  ],
  cleanUrls: true,
  ignoreDeadLinks: true,
  themeConfig: {
    logo: "/icon/logo.png",
    outline: { level: [2, 3], label: "页面导航" },
    socialLinks: [
      { icon: "github", link: "https://github.com/HaitaoJin/syzero-core" },
    ],
    search: {
      provider: "local"
    },
  },
  locales: {
    root: {
      label: "简体中文",
      lang: "zh-CN",
      link: "/",
      themeConfig: {
        nav: [
          { text: "指南", link: "/guide/getting-started" },
          { text: "模块", link: "/modules/index" },
          { text: "发布说明", link: "/release-notes" },
          { text: "GitHub", link: "https://github.com/HaitaoJin/syzero-core" },
        ],
        sidebar: {
          "/guide/": [
            {
              text: "指南",
              items: [
                { text: "快速开始", link: "/guide/getting-started" },
                { text: "架构与约定", link: "/guide/architecture" },
              ],
            },
          ],
          "/modules/": [
            {
              text: "模块目录",
              items: [
                { text: "模块总览", link: "/modules/index" },
                ...moduleLinks,
              ],
            },
          ],
        },
        docFooter: { prev: "上一页", next: "下一页" },
        darkModeSwitchLabel: "主题",
        sidebarMenuLabel: "目录",
        returnToTopLabel: "返回顶部",
        langMenuLabel: "多语言",
      },
    },
    en: {
      label: "English",
      lang: "en-US",
      link: "/en/",
      themeConfig: {
        nav: [
          { text: "Guide", link: "/en/guide/getting-started" },
          { text: "Modules", link: "/en/modules/index" },
          { text: "Release Notes", link: "/en/release-notes" },
          { text: "GitHub", link: "https://github.com/HaitaoJin/syzero-core" },
        ],
        sidebar: {
          "/en/guide/": [
            {
              text: "Guide",
              items: [
                { text: "Getting Started", link: "/en/guide/getting-started" },
                { text: "Architecture", link: "/en/guide/architecture" },
              ],
            },
          ],
          "/en/modules/": [
            {
              text: "Modules",
              items: [
                { text: "Module Index", link: "/en/modules/index" },
                ...moduleLinks.map((m) => ({
                  text: m.text,
                  link: `/en${m.link}`,
                })),
              ],
            },
          ],
        },
        docFooter: { prev: "Previous page", next: "Next page" },
        darkModeSwitchLabel: "Theme",
        sidebarMenuLabel: "Menu",
        returnToTopLabel: "Back to top",
        langMenuLabel: "Languages",
      },
    },
  },
});