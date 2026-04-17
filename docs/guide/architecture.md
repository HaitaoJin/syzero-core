# 架构与约定

SyZero 采用模块化组合思想，每个 NuGet 包都是独立能力单元。

## 分层建议

1. Web 层：承载控制器、网关、协议适配。
2. Application 层：编排业务用例，组织事务与权限。
3. Core/Domain 层：聚合、领域服务、实体和值对象。
4. Infrastructure 层：数据库、中间件、外部系统集成。

## 依赖注入约定

通过实现生命周期接口实现自动注册：

1. `IScopedDependency`
2. `ITransientDependency`
3. `ISingletonDependency`

## 模块化原则

1. 仅引入当前业务需要的模块。
2. 服务治理、缓存、消息建议与环境分层配置。
3. 轻量级内置实现适合开发和中小规模场景，大规模场景建议启用专业中间件模块。