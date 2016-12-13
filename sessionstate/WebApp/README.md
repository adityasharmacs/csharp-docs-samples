### Fails

The problem is that in a normal page load, ASP.NET will invoke three calls in one second:

```
12/13/2016 09:57:35: GetItemExclusive(dbdehvfnwwkfci2ht12dyldf)
12/13/2016 09:57:35: SetAndReleaseItemExclusive(dbdehvfnwwkfci2ht12dyldf)
12/13/2016 09:57:35: ResetItemTimeout(dbdehvfnwwkfci2ht12dyldf)
```

Each one of these calls entails a read and a write.  Therefore, it exceeds datastore's
write limit of 1 write per second per entity group.  It's writing at least 3 times per second.

