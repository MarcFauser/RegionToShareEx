@rem winget publishing - disabled for now, enable once RegionToShareEx is on winget.
@rem
@rem Prerequisites:
@rem   - A public installer to point at (e.g. a signed binary from a GitHub release).
@rem   - The wingetcreate tool: https://github.com/microsoft/winget-create
@rem   - A package id reserved under your publisher, e.g. MarcFauser.RegionToShareEx.
@rem
@rem Initial submission (creates the manifest via PR to microsoft/winget-pkgs):
@rem   wingetcreate new <url-to-installer>
@rem
@rem Later version bumps:
@rem   wingetcreate update MarcFauser.RegionToShareEx -v <version> -u <url-to-installer> -s
