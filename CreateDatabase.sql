CREATE TABLE "ChannelsGroups" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_ChannelsGroups" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL
);

CREATE TABLE "Channels" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Channels" PRIMARY KEY AUTOINCREMENT,
    "ChannelsGroupId" INTEGER NULL,
    "Title" TEXT NOT NULL,
    "Description" TEXT NULL,
    "Link" TEXT NULL,
    "Url" TEXT NOT NULL,
    "ImageUrl" TEXT NULL,
    "Language" TEXT NULL,
    CONSTRAINT "FK_Channels_ChannelsGroups_ChannelsGroupId" FOREIGN KEY ("ChannelsGroupId") REFERENCES "ChannelsGroups" ("Id")
);

CREATE TABLE "ChannelItems" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_ChannelItems" PRIMARY KEY AUTOINCREMENT,
    "ChannelId" INTEGER NOT NULL,
    "ItemId" TEXT NOT NULL,
    "Title" TEXT NOT NULL,
    "Link" TEXT NULL,
    "ThumbnailUrl" TEXT NULL,
    "Description" TEXT NULL,
    "Content" TEXT NULL,
    "PublishingDate" TEXT NULL,
    "IsRead" INTEGER NOT NULL,
    "IsDeleted" INTEGER NOT NULL,
    "IsFavorite" INTEGER NOT NULL,
    "IsReadLater" INTEGER NOT NULL,
    CONSTRAINT "FK_ChannelItems_Channels_ChannelId" FOREIGN KEY ("ChannelId") REFERENCES "Channels" ("Id") ON DELETE CASCADE
);

CREATE TABLE "Categories" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Categories" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL
);

CREATE TABLE "ItemCategories" (
    "ChannelItemId" INTEGER NOT NULL,
    "CategoryId" INTEGER NOT NULL,
    CONSTRAINT "FK_ItemCategories_Categories_CategoryId" FOREIGN KEY ("CategoryId") REFERENCES "Categories" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_ItemCategories_ChannelItems_ChannelItemId" FOREIGN KEY ("ChannelItemId") REFERENCES "ChannelItems" ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_ChannelItems_ChannelId" ON "ChannelItems" ("ChannelId");

CREATE INDEX "IX_Channels_ChannelsGroupId" ON "Channels" ("ChannelsGroupId");

CREATE INDEX "IX_ItemCategories_CategoryId" ON "ItemCategories" ("CategoryId");

CREATE INDEX "IX_ItemCategories_ChannelItemId" ON "ItemCategories" ("ChannelItemId");