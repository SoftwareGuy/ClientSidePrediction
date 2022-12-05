# [1.0.0-beta.10](https://github.com/James-Frowen/ClientSidePrediction/compare/v1.0.0-beta.9...v1.0.0-beta.10) (2022-12-05)


### Bug Fixes

* adding 2nd queue for unitask so that new tasks arn't run in current tick ([ec61388](https://github.com/James-Frowen/ClientSidePrediction/commit/ec6138888afc43e80e43c1de59f7fba50b68516b))
* fixing errors for prediction time ([efdfffb](https://github.com/James-Frowen/ClientSidePrediction/commit/efdfffb161f41e165532f82fde5e05820f6665d2))
* fixing errors in world dump ([5b0cf7e](https://github.com/James-Frowen/ClientSidePrediction/commit/5b0cf7eacdbbaaa3f3efdd4f0734150ceccad7c2))


### Features

* adding method to get state from another tick ([2e00e81](https://github.com/James-Frowen/ClientSidePrediction/commit/2e00e812cff67c139723171dc4e204e396b0b382))

# [1.0.0-beta.9](https://github.com/James-Frowen/ClientSidePrediction/compare/v1.0.0-beta.8...v1.0.0-beta.9) (2022-10-01)


### Features

* adding custom UniTask awaits ([9aa9c50](https://github.com/James-Frowen/ClientSidePrediction/commit/9aa9c50fbce270d7eba69a3aa47bf1f97a8321c7))
* adding Method to prediction time ([843cd26](https://github.com/James-Frowen/ClientSidePrediction/commit/843cd262745b8436c5b9a9d4a5c63bf49d9f3686))
* adding option to add IPredictionUpdates to collection ([7b2d948](https://github.com/James-Frowen/ClientSidePrediction/commit/7b2d948d745e0c0654e692f0628c3276e7555197))

# [1.0.0-beta.8](https://github.com/James-Frowen/ClientSidePrediction/compare/v1.0.0-beta.7...v1.0.0-beta.8) (2022-09-28)


### Features

* adding access to state pointer ([8952eb0](https://github.com/James-Frowen/ClientSidePrediction/commit/8952eb03b67521a043b517104711595b784a454d))
* adding struct that makes it easier to pack bools into single byte ([23d565b](https://github.com/James-Frowen/ClientSidePrediction/commit/23d565bd284e81308905633406c923747197ba24))


### Performance Improvements

* adding AggressiveInlining for NetworkBool ([1347f35](https://github.com/James-Frowen/ClientSidePrediction/commit/1347f35e8ca7da7cda1146f923c23af8979d7baf))

# [1.0.0-beta.7](https://github.com/James-Frowen/ClientSidePrediction/compare/v1.0.0-beta.6...v1.0.0-beta.7) (2022-09-22)


### Bug Fixes

* catching exception when starting 2 players ([678e38e](https://github.com/James-Frowen/ClientSidePrediction/commit/678e38ebeddade94befa861ed3d5e6b5b39bd74d))


### Features

* adding tostring for networkbool ([e59a255](https://github.com/James-Frowen/ClientSidePrediction/commit/e59a255b3299c67371c721ef42c6842c11369369))

# [1.0.0-beta.6](https://github.com/James-Frowen/ClientSidePrediction/compare/v1.0.0-beta.5...v1.0.0-beta.6) (2022-09-16)


### Bug Fixes

* resetting tickTimer after we run too many ticks in 1 frame ([abc3f38](https://github.com/James-Frowen/ClientSidePrediction/commit/abc3f387144eaf2c4eb895ac7d21b874f117222d))


### Features

* **Debug:** adding helper class for showing after images ([cd0bab1](https://github.com/James-Frowen/ClientSidePrediction/commit/cd0bab1aa177c884f5d08e8db8dd68a5d40a5ad1))

# [1.0.0-beta.5](https://github.com/James-Frowen/ClientSidePrediction/compare/v1.0.0-beta.4...v1.0.0-beta.5) (2022-09-15)


### Features

* adding class to help with random ([ec361fc](https://github.com/James-Frowen/ClientSidePrediction/commit/ec361fc3e9b8b63b54bd13bd7a10cace4ffe4744))
* adding more events for tick runner ([8f5c494](https://github.com/James-Frowen/ClientSidePrediction/commit/8f5c494764856924a9ee185ec8e397d4e54f742f))
* adding networkbool ([3a6c63c](https://github.com/James-Frowen/ClientSidePrediction/commit/3a6c63c9c06f5939690480177b00284f7b7cba69))
* adding option to pause tick runner ([6fd9fbe](https://github.com/James-Frowen/ClientSidePrediction/commit/6fd9fbe7c998f8117ac2e403c7c47465af81472f))
* adding ring buffer class ([3d22846](https://github.com/James-Frowen/ClientSidePrediction/commit/3d22846e22f4d702269cf4b9483d902bebf43890))
* adding struct to hold current and previous input states ([9ccb81f](https://github.com/James-Frowen/ClientSidePrediction/commit/9ccb81f96522e44ce6ccab245a62bf1928bcab54))
* delta snapshot and other small changes ([e21ac62](https://github.com/James-Frowen/ClientSidePrediction/commit/e21ac6222d8b60a8d26caa859eeda8f93883851f))


### BREAKING CHANGES

* lots of changes see Update-beta5.md for main breaking changes

# [1.0.0-beta.4](https://github.com/James-Frowen/ClientSidePrediction/compare/v1.0.0-beta.3...v1.0.0-beta.4) (2022-08-08)


### Bug Fixes

* adding other meta files ([017251f](https://github.com/James-Frowen/ClientSidePrediction/commit/017251fd8fff3e149f2f13697acdac3bc76af5ba))
* moving files into asset folder ([dbf67d2](https://github.com/James-Frowen/ClientSidePrediction/commit/dbf67d280c943a94bf688ac8499148af3e3e1e01))

# [1.0.0-beta.3](https://github.com/James-Frowen/ClientSidePrediction/compare/v1.0.0-beta.2...v1.0.0-beta.3) (2022-08-08)


### Bug Fixes

* adding missing meta files ([bb04481](https://github.com/James-Frowen/ClientSidePrediction/commit/bb04481e4e109c3adf087c9c329caeb1e2744cde))

# [1.0.0-beta.2](https://github.com/James-Frowen/ClientSidePrediction/compare/v1.0.0-beta.1...v1.0.0-beta.2) (2022-08-08)


### Bug Fixes

* fixing version in release ([fc26896](https://github.com/James-Frowen/ClientSidePrediction/commit/fc268965814a73902bb158e5797040e4336c66d9))

# 1.0.0-beta.1 (2022-08-08)


### Bug Fixes

* fixing missing inputs ([0ed71e1](https://github.com/James-Frowen/ClientSidePrediction/commit/0ed71e10b8471e429f4a71ef3a4ddd1fa1c62034))
* folder layout ([88d166f](https://github.com/James-Frowen/ClientSidePrediction/commit/88d166ff7fb0ff0bcd44cc285a97de288ab89adf))
* increasing TickNotifyToken pool size ([fe8555f](https://github.com/James-Frowen/ClientSidePrediction/commit/fe8555f88b978f406eb1a2def2ce3e6ad51a342d))
* Resimulate from tick after one received by server ([4bb352d](https://github.com/James-Frowen/ClientSidePrediction/commit/4bb352d5d101f134ca70a4562baff1bbd71a9b27))


### Features

* adding early update ([43aa7f9](https://github.com/James-Frowen/ClientSidePrediction/commit/43aa7f91420783d21a748f1f0289cfe4de4f2148))
* adding event that is called at end of setup ([7b22446](https://github.com/James-Frowen/ClientSidePrediction/commit/7b22446911b9cc12f945b80a6bbb9a151450531c))
* allowing multiple prediction behaviours per object ([c840c19](https://github.com/James-Frowen/ClientSidePrediction/commit/c840c192e1cd663ac9457b4dcb499fc819c25f6e))
* pre-release ([52705f9](https://github.com/James-Frowen/ClientSidePrediction/commit/52705f9cfd2c10fd0e3fb9f808c9d91ca17aeaf5))
