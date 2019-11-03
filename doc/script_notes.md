# Notes
* __Find__ action is required only once. __Watch__ action is an exception (character must be near the object so it requires the action)
* If __close door__ action is present, a character can stuck in room so it's impossible to know whether a given script is executable 100% just by processing scripts.
* To get around the problem above, always have __open door__ action after __close door__ action or enable __autoDoorOpening__ in the settings.


