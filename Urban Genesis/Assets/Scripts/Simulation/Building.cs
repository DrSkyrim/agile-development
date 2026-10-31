using System.Collections.Generic;

namespace UrbanGenesis.Simulation
{
    /// <summary>
    /// Plain data for one building. A building stands on a strip of lots beside one road, with its front
    /// edge on the road side, and covers <see cref="Width"/> lots along the road and <see cref="Depth"/> rows
    /// away from it. The Unity layer reads this to decide what to draw; nothing in here knows about Unity.
    ///
    /// The footprint is an oriented rectangle: <see cref="Origin"/> is its front-left corner, <see cref="Along"/>
    /// runs from there along the front edge, and <see cref="Back"/> runs from the front edge towards the back.
    /// (Looking at the building from the road, Along points to the viewer's right; the designer's plan x axis
    /// follows <see cref="Along"/> and its plan y axis follows <see cref="Back"/>.)
    /// </summary>
    public sealed class Building
    {
        public int Id { get; private set; }
        public ZoneType Zone { get; private set; }
        public BuildingTemplate Template { get; private set; }
        public int Floors { get; private set; }

        /// <summary>A number the Unity layer uses to vary small details (roof direction, chimneys, ...) of otherwise identical buildings.</summary>
        public int Variant { get; private set; }

        /// <summary>People living here: two to four per home, fixed by the building's variant. Zero if there are no homes.</summary>
        public int Residents { get { return Template.Homes * (2 + ((Variant % 3) + 3) % 3); } }

        public BuildingShape Shape { get { return Template.Shape; } }

        /// <summary>Footprint size in lots.</summary>
        public int Width { get { return Template.Width; } }
        public int Depth { get { return Template.Depth; } }

        public Vec2 Origin { get; private set; }
        public Vec2 Along { get; private set; }
        public Vec2 Back { get; private set; }

        /// <summary>The four corners of the footprint, anticlockwise.</summary>
        public Vec2[] Footprint { get; private set; }

        public Vec2 Center { get; private set; }
        public Aabb Bounds { get; private set; }

        /// <summary>The lots the building stands on. Row 0 lots touch the road.</summary>
        public IList<Lot> Lots { get; private set; }

        public double WidthMetres { get { return Template.Width * RoadNetwork.LotSize; } }
        public double DepthMetres { get { return Template.Depth * RoadNetwork.LotSize; } }

        public Building(int id, BuildingTemplate template, int floors, int variant, Vec2 center, Vec2 back, IList<Lot> lots)
        {
            Id = id;
            Template = template;
            Zone = template.Zone;
            Floors = floors;
            Variant = variant;
            Lots = lots;

            Back = back.Normalized;
            Along = new Vec2(Back.Y, -Back.X);
            Center = center;
            Origin = center - Along * (WidthMetres * 0.5) - Back * (DepthMetres * 0.5);

            Footprint = MakeFootprint(center, Back, WidthMetres, DepthMetres);
            // Along x Back is always positive, so the corners above already run anticlockwise.
            Bounds = Aabb.Of(Footprint);
        }

        /// <summary>
        /// The four corners (anticlockwise) of a footprint of the given size centred on <paramref name="center"/>,
        /// whose back is in direction <paramref name="back"/>. Used to test a spot before the building exists.
        /// </summary>
        public static Vec2[] MakeFootprint(Vec2 center, Vec2 back, double widthMetres, double depthMetres)
        {
            Vec2 b = back.Normalized;
            Vec2 along = new Vec2(b.Y, -b.X);
            Vec2 origin = center - along * (widthMetres * 0.5) - b * (depthMetres * 0.5);
            return new[]
            {
                origin,
                origin + along * widthMetres,
                origin + along * widthMetres + b * depthMetres,
                origin + b * depthMetres
            };
        }
    }
}
