﻿// <auto-generated />
using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

#pragma warning disable 219, 612, 618
#nullable enable

namespace Pal.Client.Database.Compiled
{
    internal partial class RemoteEncounterEntityType
    {
        public static RuntimeEntityType Create(RuntimeModel model, RuntimeEntityType? baseEntityType = null)
        {
            var runtimeEntityType = model.AddEntityType(
                "Pal.Client.Database.RemoteEncounter",
                typeof(RemoteEncounter),
                baseEntityType);

            var id = runtimeEntityType.AddProperty(
                "Id",
                typeof(int),
                propertyInfo: typeof(RemoteEncounter).GetProperty("Id", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
                fieldInfo: typeof(RemoteEncounter).GetField("<Id>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly),
                valueGenerated: ValueGenerated.OnAdd,
                afterSaveBehavior: PropertySaveBehavior.Throw);

            var accountId = runtimeEntityType.AddProperty(
                "AccountId",
                typeof(string),
                propertyInfo: typeof(RemoteEncounter).GetProperty("AccountId", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
                fieldInfo: typeof(RemoteEncounter).GetField("<AccountId>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly),
                maxLength: 13);

            var clientLocationId = runtimeEntityType.AddProperty(
                "ClientLocationId",
                typeof(int),
                propertyInfo: typeof(RemoteEncounter).GetProperty("ClientLocationId", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
                fieldInfo: typeof(RemoteEncounter).GetField("<ClientLocationId>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly));

            var key = runtimeEntityType.AddKey(
                new[] { id });
            runtimeEntityType.SetPrimaryKey(key);

            var index = runtimeEntityType.AddIndex(
                new[] { clientLocationId });

            return runtimeEntityType;
        }

        public static RuntimeForeignKey CreateForeignKey1(RuntimeEntityType declaringEntityType, RuntimeEntityType principalEntityType)
        {
            var runtimeForeignKey = declaringEntityType.AddForeignKey(new[] { declaringEntityType.FindProperty("ClientLocationId")! },
                principalEntityType.FindKey(new[] { principalEntityType.FindProperty("LocalId")! })!,
                principalEntityType,
                deleteBehavior: DeleteBehavior.Cascade,
                required: true);

            var clientLocation = declaringEntityType.AddNavigation("ClientLocation",
                runtimeForeignKey,
                onDependent: true,
                typeof(ClientLocation),
                propertyInfo: typeof(RemoteEncounter).GetProperty("ClientLocation", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
                fieldInfo: typeof(RemoteEncounter).GetField("<ClientLocation>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly));

            var remoteEncounters = principalEntityType.AddNavigation("RemoteEncounters",
                runtimeForeignKey,
                onDependent: false,
                typeof(List<RemoteEncounter>),
                propertyInfo: typeof(ClientLocation).GetProperty("RemoteEncounters", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
                fieldInfo: typeof(ClientLocation).GetField("<RemoteEncounters>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly));

            return runtimeForeignKey;
        }

        public static void CreateAnnotations(RuntimeEntityType runtimeEntityType)
        {
            runtimeEntityType.AddAnnotation("Relational:FunctionName", null);
            runtimeEntityType.AddAnnotation("Relational:Schema", null);
            runtimeEntityType.AddAnnotation("Relational:SqlQuery", null);
            runtimeEntityType.AddAnnotation("Relational:TableName", "RemoteEncounters");
            runtimeEntityType.AddAnnotation("Relational:ViewName", null);
            runtimeEntityType.AddAnnotation("Relational:ViewSchema", null);

            Customize(runtimeEntityType);
        }

        static partial void Customize(RuntimeEntityType runtimeEntityType);
    }
}
