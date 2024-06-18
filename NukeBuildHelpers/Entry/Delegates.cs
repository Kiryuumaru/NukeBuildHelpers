using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NukeBuildHelpers.Entry.Interfaces;

namespace NukeBuildHelpers.Entry;

public delegate ITestEntryDefinition TestEntry(ITestEntryDefinition definition);

public delegate IBuildEntryDefinition BuildEntry(IBuildEntryDefinition definition);

public delegate IPublishEntryDefinition PublishEntry(IPublishEntryDefinition definition);
