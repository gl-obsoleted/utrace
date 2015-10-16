# utrace

utrace is a remote debugging console for mobile apps.

- currently works with cocos2d-x only (class cocos2d::Console specifically)
- allow sending console commands and receiving responses
- allow receiving and filtering logs (the target app should forward its logs to cocos2d::Console::log() manually)

