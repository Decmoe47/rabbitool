# 工具兔（Rabbitool）

一个用于订阅关注感兴趣的主播的最新消息的QQ频道机器人，例如b站up的动态和直播通知。

# 功能

在QQ频道中@机器人并输入`/帮助`获取所有指令格式。

## 订阅推送

- b站
    - 动态：
        - 普通动态
        - 转发动态
        - 投稿动态（视频动态、专栏动态）
    - 直播：开播、下播

- ~~推特~~：
    - ~~普通推文~~
    - ~~转发推文（转发不带文字的）/引用推文（转发带文字的）~~
    - ~~视频推文（支持获得自动转存到COS后的下载直链）~~

- 油管：
    - 视频投稿
    - 直播（仅开播）

> ※部分油管用户使用了自定义域名，想要通过地址获取channel
> id，可以通过 [https://commentpicker.com/youtube-channel-id.php](https://commentpicker.com/youtube-channel-id.php) 这个网页转换。

- 邮箱

> ※邮箱订阅格式较为特殊：
>
> `/订阅 邮箱 [邮箱地址] username=[用户名] password=[邮箱密码] host=[服务器地址] port=[端口]`
>
> 推荐使用163/126，安全限制较为松。outlook和gmail比较严，操作步骤较为繁琐。

# 配置

## bot的配置

文件名固定为`configs.yml`，放在程序的根目录下。可以将`configs_example.yml`改名为`configs.yml`后使用。

具体说明见`configs_example.yml`的注释。

## 订阅推送的配置

（下面写的是默认值）

- b站：
    - livePush: `true`（推送开播通知）
    - dynamicPush: `true`（推送动态）
    - pureForwardDynamicPush: `false`（推送无附带文字的转发动态）
    - liveEndingPush: `false` （推送下播通知）
- 推特：
    - quotePush: `true`（推送附带文字的转发推文，即引用推文）
    - pushToThread: `false`（推送到话题子频道，同时也要写`channel=[频道名]`）
- 油管：
    - videoPush: `true`（上传视频）
    - livePush: `true`（开播）
    - upcomingLivePush: `true`（预定直播间）
    - archivePush: `false`（推送直播录像）
- 邮箱：
    - address（必须，邮箱地址，修改需取消订阅后重新订阅）
    - password（必须，邮箱密码或授权码，修改需取消订阅后重新订阅）
    - host （必须，邮箱imap服务器地址，修改需取消订阅后重新订阅）
    - port （必须，邮箱imap端口，修改需取消订阅后重新订阅）
    - mailbox: `INBOX` （订阅的邮箱，修改需取消订阅后重新订阅）
    - SSL: `false`（修改需取消订阅后重新订阅）
    - pushToThread: `false`（推送到话题子频道，同时也要写`channel=[频道名]`）
    - detail: `false` （带from、to、subject、time信息）

## 查询参数

- 列出订阅：
    - allChannels: `false`（bool，列出当前频道所有子频道的指定平台的订阅）

# 其他

- log打印在`./log`文件夹内，会自动日志切割。
- 上传推特小视频时会暂存到`./tmp`文件夹内，可手动删除。
- 因为频道机器人存在宵禁机制（0点到6点），宵禁时间内一般消息会攒起来，过了之后会逐条发送；开播/下播消息因为其时效性，会直接跳过。
